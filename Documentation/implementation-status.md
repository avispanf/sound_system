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

## Runtime profiling and lifecycle tranche

The 0.5.0-preview.3 UPM package passed 182 EditMode and 140 PlayMode tests
in both Unity 2022.3.62f3 and 6000.3.21f1 (322 per version). This includes
callback reentry, target scene unload, runtime recreation, destroyed event roots,
clip-reference clearing, cooldown pruning, shutdown duck reset and playback
rejection during shutdown. The 2,000-request saturated composite burst stays
at 32 physical voices and 16 logical instances.

Bounded logical-instance and layer/plan buffer caches reduce observed warm
managed-allocation deltas to zero in the measured trigger/stop scenarios with
debugger disabled. Cold starts, changing content shape, logging, music and IO
are not covered by that observation. CPU timings are editor observations, not
a stable speedup or target-device budget. See runtime-profiling.md.

Release notifications now run after operation collection mutations; a raw
Voice can already be reused, so callbacks should retain SoundHandles when
acting on playback ownership. The original pre-existing EditorSettings change
remains separate. Cloud CI licensing and human listening remain open.

## Addressed loading and pause verification

The 0.5.0-preview.4 core package passed 200 EditMode and 148 PlayMode tests
in both Unity 2022.3.62f3 and 6000.3.21f1 (348 per version). Six new bank
regressions initially failed and now pass; further cases cover shared pending
loads, completed-cache ownership, stale generations, loader replacement,
callback failure isolation, retries, shared references, nested event discovery,
atomic addressed playback and shutdown ordering. Listener-pause scenarios cover
virtual lifetime, VO/duck state, music boundaries and opt-out menu events.

The optional Addressables 1.21.21 assembly and validation helper compile and
activate on both editors. This is not a real catalog/bundle load test. The
provider's final late-handle validity guard was separately compiled in those
checks; it is excluded from the default core test assembly set. Runtime loading
behaviour was exercised with deterministic providers and native test clips.

See loading-and-pause.md for current bank loading/unloading contracts and optional
assembly references. The following tranche adds the previously missing playback
leases and scene scopes. Device/OS, real content and listening gates remain open.


## Playback ownership and scene-scope verification

The 0.5.0-preview.5 package is verified through installed UPM tarballs in both
Unity 2022.3.62f3 and 6000.3.21f1: 204 EditMode and 165 PlayMode tests, 369 per
version. Twenty-one added scenarios cover independent/idempotent bank leases,
manual/scoped ownership, pending cancellation, force-reset generations, loader
conflicts, multiple live handles, virtual playback, natural/composite completion,
queued VO, scheduled stingers, music cancellation and fade tails, destroyed assets,
shared direct/streamed ownership, playback predating bank loading and actual
additive-scene unload. Native unloading
uses a temporary imported WAV rather than a runtime-generated clip.

Audio Bank Scope uses the native Inspector with a compact readiness message and
locks its serialized bank list in Play Mode. Code can replace Banks through its
property, which releases/reacquires ownership. Inspector compilation is covered;
full visual/interaction sign-off is not claimed. Existing warm-allocation/load
checks remain in the complete PlayMode suite.

Real Addressables bundles/catalogs, player/device execution, audio-device recovery,
real demo content and human listening remain open. Cloud CI credentials are still
absent. No new platform or audible-quality claim is made by these editor checks.


## Decibel authoring verification

The 0.5.0-preview.6 package passes 220 EditMode and 169 PlayMode tests per local
editor (389 total): Unity 2022.3.62f3 and 6000.3.21f1. Sixteen new EditMode cases
cover amplitude ratios, serialized legacy YAML without the new fields, deterministic
dB variation, unchanged linear storage, quiet/silent values and live UI Toolkit
panel binding with text/fader multi-edit plus Undo/Redo. Four new PlayMode cases
cover inherited gain, runtime music changes without asset edits, actual mute,
occlusion and source-volume clamping. Existing performance/lifecycle checks remain
in the complete suites.

Unity 6 dark-theme Inspector screenshots were checked at 400 and 320 points wide.
Variation labels were shortened after the narrow check showed truncation. The QA
used transient event copies and a temporary window; no authored demo asset changed.
Light theme, full IMGUI interaction, screen-reader usage and human listening are
not covered by these checks. Real demo content, device/player validation, real
Addressables catalogs and cloud CI license credentials remain open.


## Design 02 Sound Event workflow

Implementation is based on the supplied CODEX-HANDOFF.md/design-spec.md and the
current source, not the HTML sample data. Preview 0.5.0-preview.7 is checked as an
installed UPM tarball in Unity 2022.3.62f3 and 6000.3.21f1. Per editor: 237 EditMode
and 172 PlayMode tests (409 total). Seventeen new Editor scenarios cover the workflow
and native binding/Undo/prefab semantics; three new PlayMode cases capture actual
runtime diagnostic context. The existing full regression/performance suites remain.

Native checks use temporary QA assets from the generated demo WAVs. Source preview,
stop-on-source-change, saved -12 dB after selection changes, pin independence,
empty search and 620-point Browser / 300–360-point Inspector were checked. Dark and
light themes were inspected; their native screenshots are separate deliverables.
No production audio or human listening approval is claimed. At that checkpoint, Music screen refinement,
a fuller Diagnostics table, inline weights, broad accessibility, target devices,
real Addressables content and cloud CI credentials remain separate work.

## Design 02 follow-up authoring

Preview.8 completed Music scene-state UI and Diagnostics columns: 239 EditMode
and 172 PlayMode tests passed on Unity 2022.3.62f3 and 6000.3.21f1.

The subsequent container authoring tranche introduces a native SoundEventChild
property drawer for Layered/Switch: Event, Label and dB Gain are directly visible;
State appears for Switch or mixed parent modes. Hidden State values are preserved.
Both UI Toolkit and IMGUI paths use the existing serialized fields. The native
array still owns add/remove/reorder, so event/state/gain travel together. Inline
Simple clip weights, broad keyboard/accessibility and device/listening checks
remain open.

## Weighted Simple authoring

Preview.10 adds source-labelled weight fields for one selected SoundEvent.
Audio clips precede clip references, matching the existing runtime slot contract.
Reordering sources still requires reviewing weights. Missing entries remain 1;
editing a later slot materializes intervening defaults without duplicating the
previous weight. Viewing does not normalize legacy values or remove trailing
entries. Multi-edit retains the native serialized array. Runtime code is unchanged.

245 EditMode tests pass on Unity 2022.3.62f3 and 6000.3.21f1. The added cases cover
expansion, defaults, Undo/Redo, addressed positions, legacy values, invalid slots,
multi-edit fallback and actual field writes. Native Unity 2022.3 at 360 pt verified
keyboard numeric input, Tab to the next weight and Undo. This is a focused keyboard
check, not full accessibility approval. The preview also includes stable README
metadata to prevent an immutable-package missing-meta import error.

## Music profile refresh and diagnostic loading context

Music reconciles added/removed/replaced states and reads current state names on
request, avoiding stale destination callbacks after profile edits. Runtime actions
are disabled immediately on window creation outside Play Mode. Sounds and
Diagnostics support Cmd/Ctrl+F within the focused window; shortcuts use native EditorWindow contexts and can be rebound in Unity Shortcut Manager. Empty Diagnostics hides the stock empty
table and presents the Play Mode capture instruction.

For a selected unloaded-clip rejection, Diagnostics shows containing Sound Bank
readiness as Live now, explicitly separate from the captured rejection. The view
does not load banks or infer historical load state. Bank discovery occurs on
selection/project changes, not on every editor refresh.

## Container authoring explanations

Nested validation identifies child row numbers and optional labels along the full
path to the issue. Reused children remain valid; only an actual path cycle is
reported as circular. Switch Inspector summaries use the clamped, rounded default
parameter value and count non-null matching child events. All matching rows are
requested together, matching PlaybackPlan. The summary describes defaults only;
local/global runtime overrides can select a different state. No validation pass
changes events, weights, parameter defaults or runtime playback behaviour.

## Container diagnostics and dependency-aware Inspector

The runtime keeps the called root event in each record and separately captures
the event that caused an unresolved source, missing Switch parameter, unmatched
Switch state, unusable source, cycle or nesting-limit rejection. NoSwitchMatch
captures the actual resolved local/global float and rounded state. Problem names
are captured separately from mutable asset names. Diagnostics opens the problem
asset and discovers containing banks for that asset; root fallback is retained
when no problem asset is available. Ready bank labels remain explicitly live.

Inspector dependency tracking observes dirty counts of nested events, Switch/Blend
parameters and attenuation presets on the native panel schedule. Unchanged graphs
do not rerun validation. External child/parameter changes refresh authoring warnings
and summaries without marking or rewriting the parent. Runtime selection, branch
acceptance, voice limits and gain behaviour are unchanged. Rejection metadata is
reset between pooled requests; ignored empty branches do not turn an accepted
request into a rejection or leak stale reasons into capacity failures.

## Desktop Player release gate

Validation tooling now imports the delivered UPM sample, validates its references,
builds a separate serialized desktop scene and executes a real Player. This is
independent of Editor PlayMode tests. Local macOS Mono / Low-stripping builds on
Unity 2022.3.62f3 and 6000.3.21f1 passed fourteen runtime checks with DSP progression
and actual AudioSource playback. The Player contains AudioMW.Runtime but excludes
AudioMW Editor and test assemblies. Muted output is not a human listening pass.

Mac IL2CPP support modules were installed for both editors. High-stripping builds
reach the native compiler gate but fail because full Xcode is absent; Command Line
Tools alone are insufficient. No IL2CPP compatibility claim is made. The helper
retains an explicit IL2CPP path for rerunning with a complete toolchain.

Package/Python checks run as a separate license-free CI job and produce the one
UPM archive consumed by Unity jobs. Fixture tarball paths are portable across
workspace mounts. Cloud Unity execution still requires credentials. Windows,
Linux, mobile/device and real Addressables catalog validation remain open.
