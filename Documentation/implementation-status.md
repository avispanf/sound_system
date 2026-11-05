# AudioMW — implementation status

2026-11-05. The authoring/runtime tranche was committed and pushed as a4f39ae.
Package source remains 0.4.0 with development recorded under Unreleased.
The distribution check uses an assembled 0.5.0-preview.1 archive; no public
release was created.

## Implemented

| Area | Delivered |
|---|---|
| Selection | Weighted random, shuffle bag, retained random/no-repeat/sequential |
| Composition | Nested Layered and Switch events, trigger-time local state, parent gain/pitch/bindings, cycle/depth rejection |
| Voice admission | Event/emitter limits, cooldown, shared groups, reject/oldest/quietest/priority policies, atomic group admission |
| Lifecycle | Logical instance owns physical and virtual layers; handle survival, group stop/steal, virtual parameter and blend persistence, pitched virtual time |
| Components | Handles in emitters, splines, occluders and scatterers; emitter/scatterer stop includes all owned instances |
| Authoring | Contextual Inspector with native serialization and multi-object support, inline validation, selected-clip creation presets and Undo registration |
| Browser | Read-only, resizable panes, scrolling preview, explicit source-clip audition, empty states, units, package-relative stylesheet lookup |
| Music | State profile, transition rules, UnityEvent controller, pending-state preview; marker refresh on transition |
| Compatibility | Reproducible minimal fixture generator and CI wiring without development-project packages |
| Product | Market research, project-license model, runtime semantics, roadmap, UI design and this status saved in Documentation |

## Verified

| Check | Result |
|---|---|
| Unity 6000.3.21f1, original project, EditMode | 176/176 |
| Unity 6000.3.21f1, original project, PlayMode | 116/116 |
| Unity 2022.3.62f3, isolated Assets package copy, EditMode | 176/176 |
| Unity 2022.3.62f3, isolated Assets package copy, PlayMode | 116/116 |
| Final container/diagnostic rerun through scoped Sound System MCP | 25/25 |
| Newly generated compatibility fixture, Unity 2022.3.62f3, EditMode | 176/176 |
| Source diff whitespace check | Passed |

There are 26 new tests: five selection tests, seventeen composite/lifecycle
scenarios and four music-state scenarios. The earlier VO test fix remains.
Test fixtures use synthesized clips; these results are not listening approval.

Visual smoke inspection used a separate Unity 2022.3 fixture: Browser at its
620 px minimum width, source waveform/metrics, Simple-event Inspector, creation
window empty state and music-preview empty state in the dark theme. It found
an unavailable AnimationCurve icon; this was replaced with a supported native
asset icon. Browser empty states and metric labels were improved after review.
Full interaction QA remains open: keyboard operation of native dropdowns was
not confirmed by automation, and Undo/multi-edit, light theme, long-name and
large-library checks were not all exercised manually. The original Sound
System editor compiles without console errors/warnings after the changes.

All Unity MCP mutations were routed to verified Sound System@0fb85599 at
/Volumes/SSD/Sound System. Other projects were not modified. The user's
pre-existing EditorSettings change (EnterPlayModeOptions 1 -> 0) was restored
after the test runner temporarily changed play-mode settings and remains
uncommitted and separate from the implementation.

## Next large tranches

1. **Distribution and CI:** The UPM archive now contains Samples~/Demo and was
   installed in clean 2022.3 and Unity 6 projects; Package Manager imported the
   sample successfully with 16 components and 29 scene dependencies checked.
   Configure secrets, run the archive CI and prepare Asset Store delivery. The workflow still targets 2022.3.62f1 and
   6000.0.58f1; those remote images/runs have not been verified. Local patch
   versions above are the evidence available today.
2. **Music and listening:** real redistribution-cleared audio is forthcoming
   from the user. Add stems/SFX, author a convincing demonstration, design
   crossfade envelopes, verify rapid transition replacement, pause/resume,
   frame stalls and device changes with actual output capture and listening.
3. **Usability and profiling:** complete UI interaction QA, measure a large
   event library and high trigger rates, add decibel-first editing and reference
   discovery, then choose Timeline/Localization integrations from pilot tasks.
4. **Commercial validation:** pilots and price tests around EUR 199/249 per
   project, final product name, support/upgrade terms, final license, Unity
   approval for a project-based Asset Store offer, store page and demo video.

The first remote CI run after a4f39ae failed at license activation. Repository
Actions secrets were inspected by name and the list was empty. No secret values
were read or created. This is independent of the green local editor checks.

There is no claim of complete FMOD/Wwise parity, zero allocations, a finished
Asset Store artifact, full platform coverage, completed UI QA or human listening.

## Assembled package verification

The 0.5.0-preview.1 development tarball was installed as LocalTarball in clean
Unity 2022.3.62f3 and 6000.3.21f1 fixtures. Each passed 176 EditMode and 116
PlayMode tests. Package Manager imported its sample; validation inspected
16 scene components, 29 scene dependencies, camera/listener presence, missing
scripts/references, shader errors and the Browser stylesheet.

The builder passed four Python tests and a repeatability check. CI now assembles
and installs the tarball before package tests. The prior Actions run stopped
at licensing; no repository Actions secrets are configured. No cloud-green
claim or Asset Store submission is made.

## Music transition verification

The subsequent 0.5.0-preview.2 UPM archive passed 181 EditMode and 126 PlayMode
tests in both Unity 2022.3.62f3 and 6000.3.21f1: 307 per version. Fifteen new
tests cover sample-buffer envelopes, Linear/EqualPower laws, cancellation,
replacement, multiple outgoing tails, intro-only targets, runtime volume,
scheduled stinger cleanup and native callback processing during a 450 ms
main-thread stall. This is automated DSP evidence, not human listening.

Crossfades use immutable DSP-time envelopes in OnAudioFilterRead on native
audio platforms. The main thread schedules clips and cleans completed groups.
WebGL uses cuts because the managed DSP path is unavailable. Crossfade UI
fields/progress/cancellation compile on both tested editors; no full visual
or interaction sign-off is claimed for this UI extension.
