# AudioMW

FMOD-level adaptive music and audio, entirely inside Unity.

**No banks. No build step. Everything in Git. Everything in the Inspector.**

AudioMW is an audio middleware layer built on Unity's own audio engine. Events,
parameters, music and mixer routing are plain ScriptableObjects, so they diff and
merge like the rest of your project and need no external authoring tool.

## Status

Pre-release. The runtime and tooling are feature complete for V1; documentation,
authored demo content and store packaging are in progress. The `AudioMW`
namespace is a working name and will change before public beta.

## What it does

**Events and containers.** Sound events with random, no-repeat and sequential
clip selection, per-voice pitch and volume randomisation, blend containers that
crossfade layers over a parameter, and a scatterer for ambience.

**Parameters.** Curve-driven modulation of volume and pitch, global or per
voice, plus routing into exposed AudioMixer values.

**Adaptive music.** A sample-accurate `dspTime` clock, gapless intro to loop
scheduling, vertical stems that fade on parameters, horizontal transitions
quantised to beats or bars, stingers, and beat and bar callbacks.

**Voice over.** Dialogue lines with queueing, priority-based interruption,
subtitle events and parameter-driven ducking.

**Spatial.** Attenuation presets with custom rolloff curves, mixer snapshot
zones, emitter gizmos.

**Tools.** Event Debugger explaining why a sound did or did not play, loudness
audit to ITU-R BS.1770, import auditor for load types and memory, live tweak
review of play-mode edits, asset browser and a runtime HUD.

## Requirements

Unity 2022.3 LTS or Unity 6. No external packages required. Optional profiler
counters activate with the `AUDIOMW_PROFILING_CORE` define when
`com.unity.profiling.core` is present.

## Quick start

```csharp
using AudioMW;

AudioSystem.Play(myEvent);
AudioSystem.PlayAtPosition(myEvent, transform.position);
AudioSystem.SetParameter(intensity, 0.75f);
AudioSystem.PlayMusic(myTrack);
AudioSystem.TransitionMusic(combatTrack, MusicQuantization.Bar);
```

See [Documentation/getting-started.md](Documentation/getting-started.md).

## Demo

Open `Assets/AudioMW Demo/AudioMW Demo.unity` and press Play. The stems there
are generated placeholders produced by `Window/AudioMW/Build Demo Content`.

## Tests

173 tests run under the Unity Test Framework: 99 edit mode, 74 play mode. The
loudness meter is calibrated against the EBU R128 reference case.
