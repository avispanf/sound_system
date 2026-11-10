# Changelog

## 0.5.0-preview.20 — Native Events workflow

- Clarified Events navigation, container selection labels and separate Review changes access while retaining existing menus and tools.
- Added actual routing/parameter references and context-gated scene-emitter audition alongside explicitly dry source preview.
- Reported failed PCM reads as unavailable instead of measured silence; retained native binding and runtime semantics.

## 0.5.0-preview.19 — Nearby audio visualization

- Added monotonic event diagnostic sequence IDs that survive buffer trimming and clearing.
- Updated the separate BAD SHIFT demo with transient nearby SoundEvent labels, a labels/details/off control and short distance-faded source links.

## 0.5.0-preview.18 — Reviewed adaptive music imports

- Added MCP music plans for synchronized stems, intensity weights and quantized MusicStateProfile transitions, confirmed in Unity.
- Preserved existing track/profile GUIDs, routes, gains and state names; rejected stale inputs and mismatched musical timing with rollback.
- Added explicit per-file Force To Mono choices and verified stereo/mono/stereo asset reimport.

## 0.5.0-preview.17 — Local agent review

- Added an optional stdio MCP companion for local attachment-to-SoundEvent plans, shared by Claude Code and Codex.
- Added an Editor-only Agent Review with explicit Unity confirmation, source hashes, preserved event references, stale-plan rejection, decode validation and failure rollback.
- Added project/session binding, request retry identity and status/receipt reporting without a remote approval operation.
- Kept mixer gains, scene/code hooks, music/VO authoring and live LUFS outside this import-only batch.

## 0.5.0-preview.16 — Reviewed mixer installation

- Added an explicit Mix Setup review/apply flow for native mixer creation, asset routes and VO ducking.
- Added mix profiles, scene scopes and shared runtime ownership with separate VO attack/release.
- Added installation receipts, stale-plan checks and rollback backups; preserved category/master faders.
- Fixed mixer write caching across different mixers with identical exposed parameter names.
- Integrated the reviewed mixer into Relay Field without duplicate source/game-code ducking.


All notable changes to this package are documented here.
The format follows Keep a Changelog and the project uses semantic versioning.

## [Unreleased]

- Add Editor-only Project Setup with a project brief, read-only audio inventory, captured library diagnostics, short recommendation explanations and explicit reviewed-profile saving. Rejecting or editing a draft writes no authored assets; stale inputs and destination conflicts block approval. Mixer generation, gains and MCP integration remain separate work.

- Add read-only Library Audit for event/music/parameter/bank/VO content with native Inspector navigation, severity filters and GUID/local-ID JSON snapshots.

- Capture nested container failure assets and actual Switch values in runtime diagnostics; route settings to the failing asset and refresh Inspector warnings when dependencies change.

- Identify nested validation issues by child path and show default Switch state matches without changing runtime selection.

- Refresh Music state buttons after live profile edits; add Cmd/Ctrl+F in Sounds and Diagnostics, clearer empty states and explicitly live Sound Bank readiness for unloaded-clip rejections.

- Source-labelled Weighted Random fields in the Inspector with native multi-edit fallback; README metadata included in UPM archives.

- Compact Layered/Switch child authoring with direct dB gain and contextual State fields; existing serialized arrays and values retained.

- Design 02 follow-up: native Diagnostics columns and persistent filters; Music workspace with profile route summaries and captured pending transition timing.

### Added

- Design 02 Sound Event workflow: persistent source Browser, native focused
  Inspector pin/follow, selected-source waveform/metrics, clear empty/loading states.
- Always-visible gain/pitch, Reset, serialized foldout summaries, and explicit
  undoable Sound Emitter assignment with prefab overrides.
- Sounds/Music/Diagnostics navigation and captured rejection context with links to
  the relevant event limits/cooldown settings.

- Decibel faders/numeric entry for event, music, VO and child gains, preserving
  linear asset storage, multi-edit and Undo/Redo, with explicit silence display.
- Relative dB volume variation for new events; old assets keep additive offsets.
- Decibel gain APIs and debugger dB/source-volume-clamp diagnostics.

- Playback clip leases spanning physical/virtual SFX, scheduled music, fade tails,
  stingers and queued speech; bank unload waits for actual resource users.
- Audio Bank Scope component and disposable bank acquisitions for additive scenes,
  with readiness diagnostics and independent manual/scoped ownership.

- SoundEvent clip references and automatic address discovery through bank event
  graphs, with atomic rejection while addressed content is not ready.
- Reference-counted cached loader base and optional Addressables assembly that
  activates from the installed package version.
- Global audio pause API and per-event Play while paused for menu SFX.

- Runtime load and lifecycle regression scenarios with calibrated allocation
  reporting and AudioMW.Play / AudioMW.Tick profiler markers.

- DSP-buffer crossfades with Linear/EqualPower curves, authored default and
  per-route durations, live fade progress and pending-transition cancellation.

- Reproducible UPM archive assembly with demo sample, user documentation,
  GUID dependency checks and tarball-based compatibility fixtures.

- Weighted random and shuffle bag clip selection.
- Nested Layered and Switch event containers with initial instance parameters,
  inherited gain/pitch/bindings and cycle/depth validation.
- Event and emitter limits, cooldowns, shared concurrency groups and four
  admission policies with whole-instance stealing and atomic voice admission.
- Contextual SoundEvent Inspector, creation presets and actionable validation.
- Music state profiles, UnityEvent-compatible controller and Play Mode
  transition preview with pending-state countdown.

### Changed

- Reuse bounded playback-instance caches, plan buffers and admission scratch
  collections; avoid allocating admission closures and unnecessary listener scans.

- Browser uses resizable panes, a scrolling preview, explicit clip audition,
  native theme styling and package-relative stylesheet lookup.
- Built-in emitter, scatterer, spline and occluder components retain handles.
  Emitter Stop stops all its overlapping instances; scatterer stop also stops
  its owned playback.

### Fixed

- Event Debugger volume chains now include occlusion instead of implying an
  unexplained difference between the factor product and final source volume.

- Destroyed music/voice assets release playback resources, and standalone stingers
  release their clips after their scheduled end without requiring active music.

- Bank loads deduplicate pending addresses, retain the accepting loader, ignore
  stale callbacks, release on destruction and clear ownership on shutdown.
- Shared references and native clips remain retained by other loaded banks.
- Virtual playback, VO ducking/queue timing and music scheduling freeze during
  listener pause; pooled voices reset their pause opt-out flag on assignment.

- Release callbacks are dispatched after runtime mutations, allowing reentrant
  playback and StopAll without collection corruption.
- Attached 2D sounds stop when their target scene unloads; destroyed event roots
  stop their complete instances. Handle IDs survive runtime recreation.
- Stop clears clip snapshots; expired cooldown history is pruned. Shutdown
  clears duck state immediately and rejects playback from release callbacks.

- Replacing or cancelling a transition no longer leaves a scheduled cut on
  the current track; earlier fading tails survive rapid subsequent changes.
- Short loop scheduling respects occupied source slots and catches up after
  a missed scheduling window; intro-only music ends correctly.
- Music volume changes affect runtime gain without writing track assets.
- Scheduled stingers reserve distinct sources; stop/destroy clean all music
  sources, queued transitions and retiring tracks.

- Demo no longer references a URP package material or additional light component.

- Composite handles remain valid after the first layer ends. Group stop,
  stealing, pool rebuild and StopAll include virtual layers.
- Virtual playback preserves instance parameters, inherited bindings, blend
  definitions and occlusion; playback advances at parameter-adjusted pitch.
- Virtual expiration cleans handle ownership even without an active listener;
  promotion never steals another voice merely to become physical.
- Browser parameter icon works on Unity 2022.3; source size is labelled PCM
  size rather than implying a measured runtime-memory footprint.
- Music markers refresh when a pending transition becomes the current track.


- Voice-over duck release test now observes duck attack and speech completion
  before requiring an exact return to zero, preventing an initial zero from
  being mistaken for a completed release.

## [0.4.0] - 2026-08-09

### Added

- Occlusion with multi ray sampling, smoothed lowpass and volume attenuation
- Full voice virtualization with hysteresis, opt in per sound event
- SoundHandle, a reference that stays valid across virtualization
- Spline emitters for rivers, roads and perimeters
- Music markers and marker quantised transitions
- Platform tier configs: Mobile 2D, Standard 3D and High-End
- Streamed clips through IAudioAssetLoader, with an optional Addressables backend
- Asset browser preview panel with waveform, transport and per clip loudness

### Fixed

- Per instance parameters now reach every layer of a blend container
- Voice pool prewarm creates the requested number of voices instead of one
- Debug HUD no longer throws under the new Input System

### Changed

- Browser styling moved to a USS sheet driven by Unity theme variables
- PlayMode tests wait on conditions rather than fixed durations

## [0.3.0] - 2026-08-07

### Added

- ITU-R BS.1770 loudness metering and library audit with CSV export
- Import auditor for load types, memory and import mistakes
- Event debugger with rejection reasons and the full volume chain
- Live tweak review of play mode edits
- Mixer routing from parameters to exposed mixer values
- Voice over layer with queue policies, subtitles and parameter ducking

## [0.2.0] - 2026-08-06

### Added

- Adaptive music: dspTime clock, gapless intro to loop, vertical layers,
  quantised transitions and stingers
- Attenuation presets and mixer snapshot zones
- Diagnostics HUD, runtime counters and the asset browser

## [0.1.0] - 2026-08-06

### Added

- Core runtime: sound events, containers, parameters, voice pool and banks
- Ambience scatterer
- Package skeleton with assembly definitions and test coverage
