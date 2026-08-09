# Changelog

All notable changes to this package are documented here.
The format follows Keep a Changelog and the project uses semantic versioning.

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
