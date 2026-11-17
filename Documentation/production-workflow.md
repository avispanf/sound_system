# Indie, mobile and A-game workflow

This scope targets Unity games with event-driven SFX, adaptive music, VO, basic
spatial audio and a small authoring team. Project budget labels do not establish
fitness: voice load, streaming, supported devices and iteration workflow do.

## Local live editing

Parameters has two distinct controls. Preview value evaluates authored response
curves without changing gameplay. Live global control is opt-in, available only
with an existing runtime in Play Mode, and writes the real global parameter store.
Local emitter/instance overrides take precedence. Asset defaults are unchanged.
Disabling the control or closing its panel restores the previous value only if
the current value still equals its last write. A different gameplay value is
preserved. Identical-value writes from another owner cannot be distinguished.

This is local Editor control, not FMOD-style remote live update to a built Player.
Play Mode asset edits remain native Inspector edits and can be reviewed separately
in Tools > Review Play Mode changes. Existing trigger-captured values such as base
voice gain or pending music transition timing are not silently recaptured.

## Spatial authoring

Assign an Attenuation Preset to a leaf Sound Event. Near/far distance and rolloff
control volume. Stereo pan is applied before native 3D panning. Spread controls
the angular extent of stereo/multichannel 3D sources, not mono-to-stereo synthesis
or a general-purpose stereo-width processor.

Distance low-pass and distance spread are optional and disabled in existing
presets. Curve X runs from 0 at near distance to 1 at far distance. Cutoff Y is Hz
(clamped to 10–22000); spread Y is degrees (0–360). A missing active listener uses
neutral filtering and the authored fixed spread. Distance processing is inactive
for 2D presets. Empty curves retain neutral cutoff/fixed spread. Spatial shows an
authored model, never a measured-output meter.

Occlusion remains multi-ray scene sampling with smoothed volume and cutoff. The
voice combines occlusion and distance filters using the lower cutoff, so clearing
occlusion cannot remove the distance filter. Existing source pooling, handles and
virtualization remain in use. A low-pass component is created lazily on first use;
measure this first-use cost as well as steady state.

## Minimum profiling

Window > AudioMW > Performance records 10 Hz snapshots for up to 600 samples and
exports CSV. Physical/virtual voices, pool limit, requests, rejections and steals
are actual runtime values. Counts cover SFX only; music and VO use separate sources.
AudioMW.Tick covers main-thread maintenance, not DSP. Main Thread and GC cover the
whole Editor frame, including editor/UI work. Unavailable counters export empty
cells, not zero. Sampling can miss short spikes; a flat graph is not proof of an
allocation-free audio system. Paused/stopped captures retain their previous samples.

For device acceptance, use a Development Player with Unity Profiler. Record the
package hash, Unity version, target/backend, device, output configuration and load
scenario. Exercise normal gameplay, worst-case bursts, long streamed ambience,
scene changes, pause/resume and first-use creation. Check CPU/DSP, audio memory,
streaming stalls, voice steals and GC against that game's frame/memory budget.
Do not infer Android/iOS performance from macOS Editor numbers.

## Production scale priorities

1. Use shared attenuation/platform profiles, explicit mixer routes, naming and
   folders; existing native multi-edit and Undo support bulk authoring.
2. Run existing library/import/loudness audits and resolve missing references,
   routing and import warnings before delivery. Authored references do not prove
   that an asset is unused: dynamic code references are outside that inventory.
3. Add representative large-library and content-change regression scenarios,
   automated device smoke runs and per-platform import-budget reports next.
4. Improve safe bulk edits with a human-reviewable diff and stable IDs before
   adding large authoring features. Agent plans already require human confirmation.

Production automation must not silently normalize all clips or set one universal
LUFS target from a genre/platform answer. Use the brief to propose budgets and
presets; measure representative gameplay, explain the tradeoffs and obtain approval.
Full live master LUFS monitoring is not implemented by the Performance window.

## Platform boundary

Platform presets are configuration, not certification. Fresh evidence is listed
in the acceptance report for each candidate. Older Unity 2022.3, macOS Mono and
PlayMode results do not certify newly changed code. Android/iOS device profiling,
Windows/Linux Players, IL2CPP/stripping, production Addressables catalogs and
audio-device interruptions still require explicit validation.

Unreal integration remains a separate future plugin. This Unity runtime/editor
and its MCP-assisted asset plans do not provide an Unreal audio backend.

Mobile 2D currently requests 16 pooled / 12 native real voices, 24 kHz and a
1024-sample buffer; it disables spatial audio, occlusion and custom DSP.
Standard 3D requests 32 voices, 48 kHz and 512 samples. High-End requests
96 pooled / 64 native real voices, 48 kHz and 256 samples, prewarming 24.
These are starting presets, not universal budgets. Hardware may negotiate audio
configuration. In preview.45 the spatial/occlusion flags are also enforced at
voice playback; distance low-pass respects CustomDspEnabled. Tier application
rebuilds the pool, so switch presets at setup rather than during active content.

For a mobile 3D game, create a measured 3D tier instead of choosing Mobile 2D
just because the target is a phone. The 24 kHz option also trades bandwidth for
quality; do not apply it indiscriminately to music-heavy games.

## Studio authoring (preview.46)

Open **Window > AudioMW > Studio** for the primary authoring workflow. The left
browser has Events (including music and voice), Banks, Assets and Parameters.
The center shows the selected asset's actual container sources and an Audio Bin
with cached waveform, file metadata and dry source audition. Selecting a source
row changes the source below; it never triggers playback automatically.

Properties are the actual native custom Inspector embedded in Studio, with
serialized bindings and Undo. The external Inspector and pin/follow remain
available. Routing / audition shows real output and parameter references plus
scene-bound runtime audition; Play source (dry) remains a separate operation.
At narrow widths Properties moves below the source deck. Banks link to their
member events. Assets lists real AudioClips; import settings still use Unity's
native Apply / Revert Inspector instead of being silently applied on selection.

**Mixer** opens Unity's native Audio Mixer as a separate window with the real
buses, effects and snapshots. This is not a new custom mixing engine. Diagnostics,
Review changes and specialist tools remain available; their content has not all
been rewritten as embedded Studio views. No FMOD timeline, instrument trigger
regions, fake signal meters or live LUFS are implied by this layout.

Reference: FMOD Studio's browser/editor/deck composition and Audio Bin workflow:
https://www.fmod.com/docs/2.04/studio/authoring-events.html
https://www.fmod.com/docs/2.04/studio/managing-assets.html
