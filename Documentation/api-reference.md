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
over global ones and are cleared when the voice is released.

## Banks

```csharp
void LoadBank(SoundBank bank)
void UnloadBank(SoundBank bank)
```

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
`GetBeatIndex`, `GetBarIndex`, `GetBeatInBar` and `GetNextBoundary`.

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

## Components

`SoundEmitter`, `SoundScatterer`, `MixerSnapshotZone`, `AudioDebugHud`,
`DemoDirector`.
