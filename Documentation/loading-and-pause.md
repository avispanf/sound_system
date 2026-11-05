# Addressed audio, ownership and pause

## Address an event

Simple events now accept ClipReferences in addition to their direct Audio clips.
Use the Addressed clips foldout in the Inspector. Each reference can hold a
direct clip or an address; a direct clip takes precedence. Selection and weights
use direct Audio clips first, then ClipReferences in their authored order.

Include the event in a SoundBank. Bank loading discovers references in nested
Layered/Switch children, deduplicates addresses within the bank and rejects
cycles during playback. Collection itself is cycle-safe. All references needed
by the selected playback path must resolve and have loaded audio data before
playback. Otherwise Event Debugger reports RejectedClipNotLoaded and no part of
the composite event is started. Retrying playback is an explicit gameplay action.

1. Assign an IAudioAssetLoader to AudioSystem.AssetLoader.
2. Call AudioSystem.LoadBank(bank).
3. Poll bank.IsFullyLoaded with a loading UI and a bounded timeout/error path.
4. Start events when ready. IsStreamingComplete only means no pending loader
   callbacks; it does not establish that every address succeeded or audio data
   has finished loading.
5. Call AudioSystem.UnloadBank(bank) when the loading owner no longer needs it.
   Existing AudioMW playback retains its selected clips until completion or stop.
   Addressed references become unresolved when the last bank owner leaves, so
   new addressed playback requires loading the bank again.

SoundBank assets are ScriptableObject loading groups, not compiled binary banks.
The LoadOnStartup field does not discover or load otherwise unreferenced assets;
explicitly reference and load the required banks from scene/bootstrap code.
MusicTrack and VoiceLine still use direct AudioClip fields; automatic event
reference discovery currently covers SoundEvent graphs.

## Ownership

CachedAudioAssetLoader is a reusable provider base. Each accepted LoadAsync
request acquires one ownership count, including a cached or pending address.
Every acquisition must have one matching Release. The backend starts once per
address, fans completion out to waiting consumers and releases its handle only
after the last consumer leaves. ReleaseAll is a force reset that cancels its
pending callbacks; unload banks first so their pending state is reset as well.
Callbacks and loader access must run on Unity's main thread.

A bank retains the loader that accepted each request. Changing the facade loader
or its serialized reference array cannot send release to a different provider.
Unload/destroy invalidates the bank request before releasing; an old callback
cannot resolve a newer generation. Repeated pending loads acquire once per bank
address. Shared ClipReference objects retain separate bank owners. Failed loads
release ownership and can be retried. Callback exceptions are logged while
other cached-loader consumers still receive completion.

Clip ownership is counted across loaded banks and AudioMW playback. Physical
and virtual SFX retain the selected layer, including while paused. Completed
composite layers release independently of longer siblings. Music retains
scheduled channels, pending transitions, outgoing fade tails, intros and
stingers. VO retains accepted queued lines before they start, and releases
cancelled or finished lines. Destroyed track/line assets also trigger cleanup.

A loader acquisition for a clip remains held until the bank request is released
and no bank or AudioMW playback still owns that clip. If several providers resolve
the same AudioClip, their retired acquisitions can remain held until the clip's
last playback/native-bank owner leaves. This intentionally favours asset safety
over early release of equivalent provider handles. Native audio-data unloading
also waits for outstanding stream owners. No new allocation is required to retain
an already registered clip on the SFX trigger path. Direct playback is registered
even if it starts before a bank is loaded; unused lifetime records are recycled in
a bounded cache of at most 128 records, with all asset/provider references cleared.

Third-party AudioSources, direct calls to loader.ReleaseAll, and external
UnloadAudioData calls are outside this ownership system. Keep those owners alive
explicitly. MusicTrack and VoiceLine still hold direct clip fields; assign/play or
queue a resolved clip before releasing its bank, rather than keeping a raw clip
reference and starting playback after its last owner has already released it.
Shutdown stops AudioMW playback before force-releasing registered banks.

## Scene and code scopes

Add **AudioMW > Audio Bank Scope** to a scene object and assign its Banks list.
It acquires on enable and releases on disable, including scene unload. Two
additive scenes can own the same bank independently; duplicate entries within
one component are acquired once. Detached one-shots, music tails and speech can
finish after the scene releases its loading owner. Attached emitters still follow
their existing stop-on-disable/target lifetime rules.

The scope Inspector shows Ready, Loading audio or a useful unavailable message.
Its bank list is editable before Play Mode. `scope.IsReady` is the gameplay gate;
use a bounded wait and an error UI. Enable order starts loading early but does
not make async audio immediately ready. Failed addresses remain unavailable;
fix the cause and disable/enable the scope to retry. Configure the asset loader
before enabling a scene scope. All overlapping scopes of a bank use the same
loader; an attempted replacement throws without stealing another scope's owner.

For code ownership:

```csharp
System.IDisposable bankLease = AudioSystem.AcquireBank(bank);
bankLease.Dispose();
```

`bank.Acquire(loader)` also returns an independent, idempotently disposable lease;
its default loader is AudioSystem.AssetLoader. Legacy LoadBank/UnloadBank remain
one idempotent manual owner, separate from scopes. UnloadBank cannot evict a scene
scope. IsLoaded describes the actual native-bank hold, and ScopeCount reports
independent scoped owners. Playback can still be completing when IsLoaded is
false. Shutdown/UnloadAll invalidate existing scope tokens; disposing an old token
cannot release an owner acquired after the reset. Re-enable scope components to
acquire again after an explicit Shutdown. Treat UnloadAll as a force reset, not
normal scene cleanup.

## Optional Addressables provider

Install Unity Addressables and construct AddressableAudioAssetLoader. Its own
AudioMW.Addressables assembly activates automatically from the package version;
no global scripting define or Addressables reference is required in the core
assembly. An asmdef that constructs the provider must reference
AudioMW.Addressables; ordinary engine consumers can keep referencing only
AudioMW.Runtime. Addressables 1.21.21 provider compilation/activation was checked on
Unity 2022.3.62f3 and 6000.3.21f1. Generic caching and bank ownership were tested
with deterministic providers; a real Addressables catalog/content load was not
performed in this tranche. To reproduce the provider check, prepare a fixture
with `--addressables-version 1.21.21`, then run its editor with
`-batchmode -projectPath <fixture> -executeMethod ValidateAddressables.Run
-logFile <log>`. The helper writes addressables-compile-validation.json.

AudioMW core has no content build step. The optional provider follows Unity
Addressables' own catalog/content build workflow. Follow the official
[Addressables handle ownership guide](https://docs.unity3d.com/Packages/com.unity.addressables@2.7/manual/AddressableAssetsAsyncOperationHandle.html).

## Pause

```csharp
AudioSystem.SetPaused(true);
AudioSystem.SetPaused(false);
```

This controls Unity's global AudioListener.pause and affects all Unity
AudioSources that have not opted out. Unity freezes the DSP clock and scheduled
boundaries. Native and virtual AudioMW playback, musical fades, speech, subtitle
state, duck envelopes and VO queue timing remain paused until resumed.

For menu SFX, enable Play while paused on the leaf SoundEvent. This maps to
AudioSource.ignoreListenerPause. The flag is set on every voice assignment, so
it cannot leak through the voice pool into another event. Parameters and target
lifetime checks may still update while paused; destroying a target can still
stop its attached event. Cooldowns continue to use unscaled game time.

| Action | Behaviour |
|---|---|
| Time.timeScale = 0 | Gameplay time pauses; audio continues unless explicitly paused |
| AudioSystem.SetPaused(true) | Global DSP/audio pause; ordinary virtual voices and VO timing freeze |
| Play while paused | That leaf event continues, including its virtual lifetime if spatial |
| Shutdown | Stops owned playback and releases registered banks; does not change the caller's global pause setting |

These checks cover AudioListener pause in local editors. OS suspension,
audio-device changes, sample-rate changes, full UI interaction and human
listening remain separate release gates. See Unity's
[AudioListener.pause contract](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/AudioListener-pause.html).
