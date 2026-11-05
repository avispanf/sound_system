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
5. Stop the bank's playback users before calling AudioSystem.UnloadBank(bank).
   Playback does not yet acquire an independent loading lease. Global Shutdown
   guarantees that SFX, music and VO are stopped before registered banks unload.

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

Native clip-data holds are counted across distinct loaded banks; unloading one
bank leaves data retained by another bank. Shutdown unloads all registered banks
and a new play session resets the registry, including when domain reload is off.
Stop other playback users before individually unloading a bank; third-party
AudioSources are outside this ownership system.

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
