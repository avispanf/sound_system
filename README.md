# AudioMW

FMOD-level adaptive music and audio, entirely inside Unity.

**No banks. No build step. Everything in Git. Everything in the Inspector.**

AudioMW is an audio middleware layer built on Unity's own audio engine. Events,
parameters, music, mixer routing and dialogue are plain ScriptableObjects, so
they diff and merge like the rest of your project and need no external
authoring tool.

## Status

Pre-release. The runtime and tooling are feature complete; authored demo
content, store packaging and verification on Unity 2022.3 are outstanding. The
`AudioMW` namespace is a working name and will change before public beta.

## What it does

**Events and containers.** Sound events with random, no-repeat and sequential
clip selection, per-voice pitch and volume randomisation, blend containers that
crossfade layers over a parameter, and a scatterer for ambience.

**Parameters.** Curve-driven modulation of volume and pitch, global or per
instance, and routing into exposed AudioMixer values in decibels.

**Adaptive music.** A sample-accurate `dspTime` clock, gapless intro to loop
scheduling, vertical stems that fade on parameters, transitions quantised to
beats, bars or named markers, stingers, and beat, bar and marker callbacks.

**Voice over.** Dialogue lines with queueing, priority-based interruption,
subtitle events and parameter-driven ducking.

**Spatial.** Attenuation presets with custom rolloff curves, mixer snapshot
zones, emitter gizmos.

**Memory.** Sound banks with preload and unload, plus streamed clips resolved
through a pluggable loader with an optional Addressables backend.

**Platform tiers.** Mobile 2D, Standard 3D and High-End presets covering voice
ceiling, prewarm, DSP buffer size, output sample rate and feature toggles.

**Tools.** An asset browser with waveform preview and per-asset loudness, an
Event Debugger explaining why a sound did or did not play, loudness audit to
ITU-R BS.1770, an import auditor for load types and memory, live tweak review of
play-mode edits, and a runtime HUD.

## Requirements

Unity 2022.3 LTS or Unity 6. No packages required.

Optional integrations activate through scripting define symbols:

| Define | Enables |
|---|---|
| `AUDIOMW_ADDRESSABLES` | Addressables backend for streamed clips |
| `AUDIOMW_PROFILING_CORE` | Counters in the Unity Profiler |

## Quick start

```csharp
using AudioMW;

AudioSystem.Play(myEvent);
AudioSystem.PlayAttached(myEvent, transform);
AudioSystem.SetParameter(intensity, 0.75f);
AudioSystem.PlayMusic(myTrack);
AudioSystem.TransitionMusic(combatTrack, MusicQuantization.Bar);
AudioSystem.PlayVoiceLine(introLine);
```

See [Documentation/getting-started.md](Documentation/getting-started.md).

## Windows

Open `Window > AudioMW`:

| Window | Purpose |
|---|---|
| Audio Browser | Navigate events, music, parameters, banks and voice lines |
| Event Debugger | Why each request played or was rejected, with the volume chain |
| Loudness Audit | Integrated LUFS and true peak per clip, with CSV export |
| Import Auditor | Load types, memory estimates and import mistakes |
| Live Tweaks | Assets changed during the last play session |

## Demo

Open `Assets/AudioMW Demo/AudioMW Demo.unity` and press Play. The stems there
are generated placeholders produced by `Window/AudioMW/Build Demo Content`.

## Tests

208 tests run under the Unity Test Framework: 127 edit mode, 81 play mode. The
loudness meter is calibrated against the EBU R128 reference case; play-mode
tests wait on conditions rather than fixed durations.
