# Changelog

All notable changes to this package are documented here.
The format follows Keep a Changelog and the project uses semantic versioning.

## [Unreleased]

### Added

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
