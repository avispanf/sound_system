# API reference

Everything lives in the `AudioMW` namespace. `AudioSystem` is the static facade;
`AudioRuntime` is the singleton behind it and is created automatically.

## Playback

```csharp
Voice Play(SoundEvent evt)
Voice PlayAtPosition(SoundEvent evt, Vector3 position)
Voice PlayAttached(SoundEvent evt, Transform target)
void  StopAll()
int   ActiveVoiceCount { get; }
void  Shutdown()
```

`Play` returns `null` when the request is rejected. The Event Debugger records
the reason.

## Parameters

```csharp
void  SetParameter(SoundParameter parameter, float value)
float GetParameter(SoundParameter parameter)
void  SetVoiceParameter(Voice voice, SoundParameter parameter, float value)
```

Values are clamped to the parameter's range. Per-voice values take precedence
over global ones and are cleared when the voice is released. On a blend
container the value cascades to every layer, so two instances of the same event
can be driven independently.

## Banks

```csharp
void LoadBank(SoundBank bank)
void UnloadBank(SoundBank bank)
System.IDisposable AcquireBank(SoundBank bank)
```

AcquireBank returns an independent ownership token. Dispose it to release that
owner; repeated Dispose is safe. SoundBank.Acquire(loader) is also available,
using AudioSystem.AssetLoader by default. ScopeCount reports scoped owners;
LoadBank/UnloadBank retain their separate idempotent manual ownership.
AudioMW playback retains its selected clips across bank unload. See
[loading-and-pause.md](loading-and-pause.md) for readiness and force-reset rules.

## Music

```csharp
void PlayMusic(MusicTrack track, MusicQuantization quantization = Immediate)
void TransitionMusic(MusicTrack track, MusicQuantization quantization = Bar)
void PlayStinger(AudioClip clip, MusicQuantization quantization = Beat, float volume = 1f)
void StopMusic()
MusicPlayer Music { get; }
```

`MusicPlayer` exposes `Clock`, `CurrentTrack`, `IsPlaying`, `ChannelCount`,
`GetLayerName`, `GetLayerWeight`, `IsTransitionPending`, `PendingTrack`, and the
`BeatTick` and `BarTick` events.

`MusicClock` exposes `Tempo`, `BeatsPerBar`, `SecondsPerBeat`, `SecondsPerBar`,
`GetBeatIndex`, `GetBarIndex`, `GetBeatInBar`, `GetNextBoundary` and
`GetNextMarkerTime`. `MusicPlayer` also raises `MarkerReached`.

## Voice over

```csharp
bool PlayVoiceLine(VoiceLine line, VoiceOverMode mode = Queue)
void SkipVoiceLine()
void StopVoiceOver()
VoiceOverDirector VoiceOver { get; }
```

Modes are `Queue`, `Interrupt` (only when the incoming priority is not lower)
and `IgnoreIfBusy`. The director exposes `IsSpeaking`, `CurrentLine`,
`QueueLength`, `DuckValue`, `DuckParameter`, `DuckFadeSeconds` and the
`LineStarted`, `LineFinished` and `SubtitleChanged` events.

## Tiers and loading

```csharp
void ApplyTier(AudioTierConfig config)
AudioTierConfig ActiveTier { get; }
IAudioAssetLoader AssetLoader { get; set; }
```

`AudioTierConfig` exposes `MaxVoices`, `PrewarmVoices`, `DspBuffer`,
`OutputSampleRate`, `RealVoiceCount` and the `SpatialAudioEnabled`,
`OcclusionEnabled` and `CustomDspEnabled` toggles, plus the `Mobile2D`,
`Standard3D` and `HighEnd` factory presets.

`SoundBank` adds `StreamedClips`, `LoadStreamed`, `UnloadStreamed`,
`PendingStreamCount` and `IsStreamingComplete`.

## Mixing

```csharp
void AddMixerRouting(MixerRoutingProfile profile)
void RemoveMixerRouting(MixerRoutingProfile profile)
MixerDirector Mixing { get; }
```

`MixerDirector` writes only when a value actually changed, and can transition or
blend snapshots.

## Diagnostics

```csharp
EventDebugger Debugger { get; }
```

`EventDebugger` holds a bounded record list with `Filter`, `CountWithOutcome`,
`TryGetLast`, `Clear`, `Enabled` and `Capacity`. Each record carries the outcome,
clip, position and the volume chain.

## Loudness

```csharp
LoudnessResult LoudnessMeter.Analyze(float[] interleaved, int channels, int sampleRate)
```

Returns integrated, momentary and short-term LUFS, sample peak and true peak in
decibels, and `OffsetToTarget` for normalisation planning.

## Assets

| Asset | Menu |
|---|---|
| `SoundEvent` | Create > AudioMW > Sound Event |
| `SoundParameter` | Create > AudioMW > Parameter |
| `SoundBank` | Create > AudioMW > Sound Bank |
| `MusicTrack` | Create > AudioMW > Music Track |
| `VoiceLine` | Create > AudioMW > Voice Line |
| `AttenuationPreset` | Create > AudioMW > Attenuation Preset |
| `MixerRoutingProfile` | Create > AudioMW > Mixer Routing |
| `AudioTierConfig` | Create > AudioMW > Tier Config |

## Components

`AudioBankScope`, `SoundEmitter`, `SoundScatterer`, `MixerSnapshotZone`, `AudioDebugHud`,
`DemoDirector`.

AudioBankScope retains its Banks while enabled and releases on scene unload or
disable. IsReady gates playback of addressed events. Shared banks remain loaded
until their last scoped/manual owner leaves; detached playback may finish later.

## Addressed events and pause

SoundEvent.ClipReferences adds address/direct references after the ordinary
Clips array in selection order. GetClip(index), TotalClipCount and
HasUnresolvedReferences describe that combined content. SoundBank discovers
event references through its child graph and owns the accepting loader.
CachedAudioAssetLoader supplies shared pending/completed ownership, with
BeginLoad and EndLoad implemented by a concrete provider.

AudioSystem.IsPaused / SetPaused(bool) control Unity's global listener pause.
SoundEvent.IgnorePause opts that leaf event out. See loading-and-pause.md for
readiness, shutdown, loader lifecycle and platform verification limits.


## Decibel gain

SoundEvent, MusicTrack and VoiceLine expose VolumeDb; SoundEventChild exposes
GainDb. Existing Volume/Gain properties and serialized fields remain linear.
SoundEvent.VolumeVariationMode selects LinearOffsets or Decibels, with separate
VolumeRandomDbMin/Max values. New factory/menu events use Decibels; old serialized
events keep the zero-valued LinearOffsets default. MusicPlayer.SetVolumeDb changes
runtime gain and VolumeDb reports it. AudioGain converts amplitude ratios.

EventDebugRecord includes OcclusionMultiplier, UnclampedVolume, FinalVolumeDb,
WasVolumeLimited and DescribeVolumeDecibels alongside the linear chain. See
[gain-authoring.md](gain-authoring.md) for silence, clamps and compatibility.

### Captured container failure context

`EventDebugRecord.Event` remains the requested root. `Rejection.ProblemEvent`
identifies a failing nested event when available, and `ProblemEventName` retains
its name at capture. `Rejection.ContentIssue` distinguishes missing Switch
parameters, unmatched states, unusable sources, cycles and nesting limits.
`SwitchValue` and `SwitchState` are meaningful only for
`PlaybackContentIssue.NoSwitchMatch`; they contain the actual parameter value and
rounded state at request time, not a later Inspector/default value. Existing
`PlaybackOutcome` values and accepted-but-silent semantics remain unchanged.
