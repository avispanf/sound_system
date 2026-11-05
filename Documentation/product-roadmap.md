# AudioMW product goal and release roadmap

Updated 2026-11-05. AudioMW is a Unity-native audio middleware package for solo
developers and small teams. The first commercial release should handle common
indie sound-design and adaptive-music workflows with predictable behaviour,
clear diagnostics and little project-specific code. Unreal is a later product
milestone, after Unity demand is validated.

The package is `com.avispanf.audiomw`, version 0.4.0, namespace `AudioMW`.
The repository is `avispanf/sound_system`; the working project is
`/Volumes/SSD/Sound System`. AudioMW remains the working product name.

## Verified baseline

Local batchmode results on 2026-11-05:

| Environment | EditMode | PlayMode |
|---|---:|---:|
| Original project, Unity 6000.3.21f1 | 176/176 | 116/116 |
| Isolated package copy under Assets, Unity 2022.3.62f3 | 176/176 | 116/116 |

The isolated 2022.3 project uses Test Framework 1.1.33 and the required built-in
modules. It excludes the original project's Unity 6 project dependencies and
demo assets. These checks do not establish player-build support, demo import,
audible quality or CI success.

`DuckParameterFallsAfterSpeech` previously accepted an initial zero before
the speech and duck release had occurred. It now waits for attack, speech
completion and exact zero, with a bounded release wait. Runtime ducking was
not changed. The existing `EditorSettings.asset` modification remains a
separate user change.

## Release gates

| Gate | Work | Acceptance evidence |
|---|---|---|
| R1 Package installation | Define the shipped Asset Store/UPM layout; assemble `Samples~/Demo` in the distribution archive; include documentation and licenses | Import the actual distribution into clean supported-version projects; open sample; no missing references or package compile errors |
| R2 Compatibility | Use minimal version-specific CI fixtures; configure CI credentials separately; verify optional integrations | Green CI for both modes on declared versions; player smoke checks on advertised platforms; optional defines both off and on with their dependencies |
| R3 Listening | Replace generated demo placeholders with redistribution-cleared audio | Human listening sign-off for intro/loop, music transitions, crossfades, VO release, interrupt/skip and scene changes; record devices and settings |
| R4 Lifecycle and performance | Exercise pause/resume, scene unload, application focus, device/sample-rate changes and domain reload settings | Stable handles, released resources, no unwanted playback or stuck ducking; measured CPU, memory, voice counts and allocation behaviour on target devices |
| R5 Customer workflow | Run pilots in external indie projects | Main tasks completed without custom code or repeated support; adoption and willingness to pay recorded |
| R6 Store delivery | Final name, accurate claims, demo video, documentation, support route and license | Published claims match verified versions/platforms/features; clean installation from the submission artifact |

CI now generates a minimal package-only project through
Tools/prepare_test_project.py before running the existing version matrix
(2022.3.62f1 and 6000.0.58f1). This excludes the development project's URP 17.3.0
and other Unity 6 dependencies. Remote CI, credentials and image availability
remain unverified; local checks used 2022.3.62f3 and 6000.3.21f1.

## Loading and pause tranche, 2026-11-05

Implemented bank-owned async requests with stale-callback rejection, shared
loader/clip ownership, graph reference discovery, addressed SoundEvent playback
and shutdown ordering. Added global listener pause and opt-out menu events;
virtual lifetime, VO and music timing freeze consistently. Optional Addressables
assembly activation compiles with package 1.21.21 on both local editors. Real
catalog/bundle loading and OS/device suspension remain future work. Playback
leases and scene bank scopes are covered by the subsequent tranche below. See loading-and-pause.md.

## Decibel authoring and gain diagnostics, 2026-11-05

Implemented dB faders for event/music/VO/child gain without changing stored linear
values; explicit silence and mixed selection, native Undo/Redo, and opt-in relative
dB variation with new-event defaults. Old additive variation retains its behaviour.
Debugger now includes occlusion, source-volume clamping and a dB view. Narrow dark
Inspector screenshots were checked and labels shortened. See gain-authoring.md.

## Playback leases and scene scopes, 2026-11-05

Added clip ownership for physical/virtual SFX, composite layers, music channels,
pending transitions, outgoing tails, stingers and accepted VO queues. Bank unload
releases its loading ownership while playback completes safely. Native and
provider-loaded clips share a registry, including direct banks using a resolved
clip. AudioBankScope and disposable bank leases support overlapping additive
scenes; manual loading is a separate owner. The scope Inspector reports loading
readiness without adding an authoring window. See loading-and-pause.md.

## Runtime lifetime and profiling tranche, 2026-11-05

Added reentrant release notification dispatch, runtime-wide handle ID lifetime,
scene-target cleanup for 2D/3D, immediate duck reset at shutdown, cooldown
history pruning, bounded playback/plan caches and profiler scopes. Warm trigger
measurements and saturated composite bursts are now reproducible in tests.
See runtime-profiling.md for scope and results. Player/device CPU, resident
memory, streaming and large-library UI profiling remain open.

## Music transition tranche, 2026-11-05

Implemented DSP-buffer Linear/EqualPower crossfades, route/default durations,
pre-boundary cancellation, rapid replacements with independently retiring
tracks, runtime-only music volume, safe queued stingers, and frame-stall tests.
The preview shows fade gain/countdown and disables cancellation after commitment.
Native platforms use OnAudioFilterRead; WebGL uses cuts. Human listening,
target-device measurements and audio-device recovery remain release gates.

## Implementation tranche, 2026-11-05

Implemented in the working tree: weighted random and shuffle bag selection;
nested Layered and Switch containers; instance ownership across physical and
virtual layers; event/emitter/group admission and cooldowns; atomic voice
reservation and group stealing; initial local parameters; handle-backed
emitters, scatterers, splines and occluders; contextual SoundEvent Inspector;
creation presets; resizable read-only Browser; music state profiles and a
Play Mode transition preview. See authoring-and-runtime.md for semantics and
ui-design.md for the interaction design. Test evidence is recorded separately
in implementation-status.md after verification.

Still planned: richer
VO presets, reference discovery, Timeline/Localization integrations selected
by pilots, performance measurements, player builds and human listening.
Do not treat this tranche as completion of every release gate.

## Next development milestones

### Container authoring and voice limits

Build on the existing Simple/Blend modes and Random/RandomNoRepeat/Sequential
selection. Add switch/state selection, weighted choices, shuffle bag, layered
events and reusable container composition in measured increments.

Define per-instance versus shared selection state, parameter inheritance,
cycle rejection, stop ownership and handle behaviour before adding nesting.
Verify that surface selection, layered weapons and ambience behave
predictably with multiple simultaneous emitters.

Introduce per-event and per-emitter limits, cooldowns and concurrency groups.
The current pool steals the oldest voice globally. New stealing/rejection
policies must respect authored importance and explain each decision in the
existing Event Debugger. Priority set on AudioSource is not a substitute for
the package's own pool policy.

### Sound designer workflow

Keep Audio Browser read-only. Use the Inspector and a dedicated setup wizard
for authoring. Provide presets for footsteps, weapon layers, UI, ambience and
VO ducking, with multi-edit, Undo, validation and reference discovery.

Expose decibel-oriented controls where appropriate, while preserving asset
serialization and existing authored values. Preview parameter changes without
silently modifying source assets. Show the final gain calculation and the
reason a sound was rejected or virtualized.

### Adaptive music workflow

Expose the existing musical clock, markers, layers, stingers and quantized
transitions through a readable transition table and preview. Show the next
scheduled boundary and the outgoing/incoming states.

Acceptance scenarios include exploration to combat, rapid state changes,
victory stingers and dialogue over music. Test frame stalls and long playback;
measure actual rendered transitions before claiming sample-level accuracy of
every crossfade or callback.

### Integrations selected by pilots

Prioritize Timeline, Animation Events, UnityEvent triggers, Localization and
Addressables according to actual customer tasks. Keep integrations optional.
Provide a previewable migration path from simple Unity AudioSource setups.
Do not promise automatic conversion of arbitrary FMOD or Wwise projects.

## Commercial hypothesis

Start validation around EUR 199 before VAT per project for a finished first
release; test EUR 249 after strong demos and external adoption. An optional
paid beta offer around EUR 149 is a separate experiment. Asset Store pricing
is set in USD, so these figures are economic targets rather than configured
store prices.

Use comparable direct-site and store offers. Treat tax, refunds, support and
customer acquisition separately from the store's revenue share. The agreed model is one perpetual purchase for one project and its whole team,
without subscriptions or royalties. It does not cover every future game from
the studio. See commercial-model.md for proposed project boundaries, updates
and the unresolved Asset Store licensing approval.

See [market and pricing research](market-and-pricing-2026-11-05.md) for source
links, market evidence, tax scenarios and the pilot design. No price, store
listing, payment account or external outreach has been created by this work.

## Unreal boundary

Keep portable specifications for events, parameters, container evaluation,
music transitions and voice policies. Preserve common behavioural test cases.
Defer a generic engine abstraction until a concrete Unreal prototype needs it.
An Unreal implementation must justify its workflow alongside MetaSounds and
Quartz, and requires a separate runtime/editor integration.

## Working conventions

- Confirm the Sound System instance before any Unity MCP write. Never modify
  Hit.Zone or AnimaClient tooling for this project.
- Preserve unrelated working-tree changes.
- Audio Browser is read-only; use `EditorGUIUtility.IconContent` for icons and
  clip GUIDs for waveform cache identity.
- Do not add code comments; unavoidable comments must be English.
- Prefer complete implementations with explicit ownership and lifecycle.
- Treat automated checks and human listening as separate release evidence.
