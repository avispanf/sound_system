# Spatial, Parameters and Routing — native workspaces

2026-11-10, preview.21. Continues the v1.1 editor guide in three dockable Unity windows. Shared navigation opens the corresponding workspace rather than a placeholder or Mix Setup. Mix Setup and human-confirmed Agent Review remain separate tools. Asset browsing, graph markers and selection do not write assets, play audio or modify parameters.

Spatial accepts SoundEvent and AttenuationPreset assets. Parent containers explain why each leaf has its own curve. Presets override event spatial properties. The graph labels X in metres (one Unity unit assumed), Y as amplitude, and shows the selected distance. A preview marker is explicitly separate from a matching active scene SoundEmitter and AudioListener pair; real positions supply distance only when both are valid. Custom curves without an authored preset are unavailable, not replaced by invented data. The curve is a model, not a measurement of output: spatialization, occlusion, mixer effects and master gain remain outside it. Native SoundEmitter Scene View gizmos remain authoritative for shape.

Custom curves use the AudioSource distance/maxDistance axis. Logarithmic model distance is not cut off at maxDistance. This follows the source configuration and the [Unity AudioSource inspector reference](https://github.com/Unity-Technologies/UnityCsReference/blob/master/Editor/Mono/Inspector/AudioSourceInspector.cs) and [maxDistance contract](https://docs.unity.com/ja-jp/engine/6000.0/script-reference/unityengine/audiosource/maxdistance). The existing AttenuationPreset evaluator used elsewhere is not silently rewritten. No DSP parity or listening measurement is asserted for the graph.

Parameters shows the asset range/default/authored scope, a view-only preview slider, and a separate global-store readout in Play Mode. An unset store is explicitly distinguished from zero and from its default fallback; per-emitter overrides are unavailable here. Asset scanning finds SoundEvent gain/pitch bindings, Blend layers, Switch state dependencies, MusicTrack layers, mixer bindings and VO duck profiles. Response foldouts evaluate full authored curves with actual parameter units and control-specific Y units. Dynamic code references are not discoverable by this asset scan. Refresh rescans references; Undo/Redo and project changes also refresh the view.

Routing traverses authored children, retains repeated leaves, labels Switch states and detects missing children, cycles and depth overflow. The displayed assignment ends at the real AudioMixerGroup and links to the native mixer. It does not fabricate upstream Master buses, sends, effects or meters. Routing profiles show actual parameter-to-exposed-control bindings, ranges and availability. Control units remain owned by the native mixer rather than assumed to be dB for every binding.

Native selection follows compatible assets unless Follow selection is disabled. Pin opens the native focused Inspector. All editing remains there, retaining serialization, Undo/multi-edit and existing runtime behavior. The flat surfaces, restrained teal accent, theme-aware graph and wrapping controls extend the current visual direction. Workspaces support a 620-pixel window with scrollable details.

Verification is recorded in SPATIAL-UI-VERIFICATION.json in the delivered BAD SHIFT project. Runtime source parity with preview.20 is checked; this Editor-only batch does not claim a fresh gameplay/native-build or listening test. Unreal, output LUFS capture, dynamic reference discovery and native mixer topology/effect introspection remain outside this batch.

Fresh acceptance: 314/314 EditMode passed on 6000.3.21f1. Native BAD SHIFT fixture inspected at 620 logical pixels in Light and Dark; Spatial preview marker changed to 7 m, parameter preview to 0.6 with the actual music-layer response, and three Footsteps_Switch routes displayed their actual groups. Editor output stayed muted, Play Mode was not entered. Original Dark theme was restored. No fresh native gameplay build or listening pass is claimed.

## Music and Diagnostics polish (2026-11-10)

Music uses the shared native typography and teal context cards, with a separate captured scheduling section and a vertically scrollable workspace. Current/Pending remain unavailable before runtime starts. Scheduled quantization, fade and curve still come from MusicPlayer. Scheduler and runtime sources are unchanged.

Diagnostics has a compact header, scrollable captured details, explicit accepted-without-gain-capture wording, and a source-chain scope disclosure. Settings navigation provides a reason when its captured asset is unavailable. Accepted request counts do not imply audible playback.

Validation: 314/314 EditMode tests and 5/5 package tests passed before the final ordering-only fix that preserves the DSP-platform warning below pending timing. Final delivery is rerun separately in music-delivery-edit.xml. Native Unity 6000.3.21f1 screenshots cover both windows at 360 logical pixels in Dark, with real Music_States and the genuine empty Diagnostics state. Editor master mute was enabled before opening RelayField. Light theme verification remains incomplete: repeated native dropdown actions did not open the theme menu. Dark was left unchanged. No PlayMode, populated-record visual acceptance, listening pass or native player rebuild is claimed for this editor-only batch. Vertical layer meters and a graphical musical timeline remain future work; no illustrative measurements were added.

## Dark-theme review and runtime acceptance (2026-11-11)

The latest user decision keeps Dark as the visual acceptance target; Light is no longer part of this batch. Native Unity theme preferences are not overridden by the package.

Diagnostics switches below 520 logical pixels from three columns to event/outcome plus a separate reason line. It restores columns when widened. Full captured identity remains in details. Headers have consistent insets; Spatial, Parameters and Routing now use the same eyebrow/title order as Events and Music, and their library rows have horizontal padding. Diagnostics refreshes when leaving Play Mode and rechecks availability of the settings target without rewriting captured values.

The isolated BAD SHIFT fixture ran real AudioMW requests with editor master mute and listener output zero. A temporary copy of Music_States and its existing tracks used a deliberately slow 1 BPM clock for inspectable Pending screenshots; it was not an authored-content change. The probe submitted actual UI requests, verified repeated requests preserve the boundary, captured fade remains 2 seconds after editing the temporary profile to 9, cancellation before the boundary, disabled cancellation after it, a real crossfade and completed destination. Real Impact requests produced accepted, cooldown-rejected and accepted-zero-source-gain records. No mock debugger records or fabricated meters were used.

Native screenshots cover Music and populated Diagnostics at 360 logical pixels, and Events, Spatial, Parameters and Routing at 620. Diagnostics was also inspected after exiting Play Mode: records clear and unavailable guidance returns. The test fixture and package sources preserve runtime bytes. This is editor/runtime inspection with muted output, not a listening check or a new native player build.

Fresh checks passed: 315/315 EditMode, 32/32 targeted PlayMode (music states/transitions/crossfades and debugger), 5/5 package tests and 16/16 real runtime UI assertions. Evidence is retained in work/bad-shift/review23-final-edit.xml, review23-play.xml, music-runtime-acceptance.txt and the outputs/audiomw-*-runtime-dark.png screenshots in the development workspace. The reproducible acceptance helper is archived in work/bad-shift/MusicRuntimeAcceptance.cs and excluded from the delivered project.

## Native Inspector and Review changes (2026-11-16)

Dark remains the visual target. Native SoundEvent Inspectors retain serialized binding, Undo and multi-edit. At narrow widths, collapsed section summaries occupy their own line and gain controls use a slider above the numeric/reset/mute row. No ranges, serialized values or playback semantics change.

Review changes now supports a 360 px window, separates its confirmation actions from the scrollable proposal, shows the captured before state and proposed source filenames, and folds file paths/checksums away by default. Connection details are a separate foldout. Long item headings and buttons wrap. The existing human confirmation dialog and validation before apply are preserved. The backend still applies a whole plan; the UI explicitly explains that scope changes require a revised proposal. Per-item selection and partial application remain outside this UI batch.

Fresh validation: 317/317 EditMode tests (including gain-control bounds at 300 px, Review action bounds at 360 px, pending plan inspection without import, existing Undo/multi-edit tests) and 5/5 package tests. Native screenshots and inspection are recorded separately in the delivery verification file. No listening test, new gameplay build or new PlayMode result is claimed for these editor-only changes. Temporary native acceptance proposals must remain pending and never be approved by automation.

### Reference composition pass

Review uses a graphite three-column layout: proposal list, change cards and review context. Below 900 px, proposal/context panels reflow into the content scroll while confirmation actions remain outside it. Event cards show captured source state, preserved settings and proposed filenames. Reasons, file hashes and limitations remain accessible in foldouts. The primary action keeps the existing human-only confirmation and whole-plan scope.

The native Inspector uses reference dividers, graphite surfaces and a teal gain fader. Source clips collapse independently; serialized fields, multi-edit and Undo remain native. At narrow widths gain occupies the full column so Reset/Mute labels remain intact. Diagnostic navigation waits for section expansion layout before scrolling to the target field. Its test now uses a scrolling Inspector host and checks viewport bounds.

Native evidence from Sound System: outputs/audiomw-inspector-300-dark.png, audiomw-review-wide-dark.png and audiomw-review-360-dark.png in the development workspace. One real pending proposal was inspected without importing or applying; the temporary bridge session was then disconnected. No scene was edited. BAD SHIFT is the isolated package test fixture. Remaining reference work includes common styling across Events/Music/Diagnostics and music/profile review cards; full visual parity is not claimed.

## Shared reference composition (preview.25, 2026-11-16)

Events keeps search/filter controls in the left project library and source/runtime inspection in the center. Shared surfaces and navigation now use the same graphite/teal palette across workspaces. Native selection, pin/follow, preview lifecycle and filters remain unchanged. At 300 px, container and selection fields stack their labels so the selected algorithm remains readable.

Music now has a searchable library of actual MusicStateProfile assets and a separate transition context. The native profile object field remains available at all sizes. Below 900 px, the library hides and context flows into the main scroll; selecting a profile and resizing never requests playback. No illustrated beat timeline or layer meters are fabricated. Current/pending, captured scheduling and transition rules continue using the existing runtime data.

Review music and profile cards share the event-card hierarchy. Exact source/stem settings and transition rules remain available, and a profile counts as a proposed asset. Human-only approval and whole-plan application remain unchanged.

Verification: 318 EditMode checks include profile/control preservation across wide/narrow layouts. Native Sound System screenshots cover Events at 620, Music at 360/1100, Diagnostics at 360, Inspector at 300 and an actual pending music/profile proposal without applying. BAD SHIFT runtime probe in a normal Unity Editor passes 16 assertions with editor output muted and AudioListener volume zero. A batch-mode attempt did not finish its DSP crossfade; it is not counted as a passing runtime check. No new native player build or listening check. Full graphical parity, live layer meters and a beat timeline are not claimed.

## Spatial, Parameters and Routing composition (preview.26, 2026-11-16)

The shared inspection workspace now follows the reference's library / overview / context composition. Search and Follow selection stay in the left library; native Inspector actions and factual context occupy the right column. Below 1000 logical pixels context moves into the central scroll without changing the selected asset. Rebuilding a window restores its library highlight.

Spatial puts the actual authored distance graph above its preview slider. Emitter/listener fields stack their labels and a matching scene distance updates even when a Custom curve is unavailable. Parameters keeps actual authored references and response curves in the overview, with range/default/scope and runtime availability in context. Routing presents actual output assignments as cards and retains native mixer links. Parent topology, effect sends and bus meters remain explicitly unavailable rather than imitating reference data.

Fresh checks: 319 EditMode tests in the isolated BAD SHIFT fixture and 5 package tests. The new layout test checks all three workspaces at 1200/620 pixels, unchanged serialized assets and selection after rebuilding. Native dark Sound System captures cover all three at wide/narrow sizes with editor audio muted. This is not a fresh PlayMode suite, listening test or native gameplay build. Runtime code is byte-identical to preview.25. Full graphical parity remains incomplete, notably live mixer topology/meters and the Music beat timeline.

## Captured diagnostics and source rows (preview.27, 2026-11-16)

Diagnostics separates the request overview from a graphite Captured detail panel at 900 px and above. Below that width the panel stacks beneath the overview with its own scroll. Existing filters, captured gain foldout, live bank readiness and navigation to the problem asset remain intact. Accepted/rejected labels retain text and now use teal/amber. The panel explicitly distinguishes historical capture from live output measurement. The reference's extra project library is not added in this pass; this remains a request browser.

Events source rows now show actual AudioClip duration and channel count beside the source name. Missing/unresolved sources display Unavailable, not zero duration. Preview selection, dry transport and runtime audition are unchanged.

Fresh verification: 321/321 EditMode tests in BAD SHIFT, including wide/narrow detail geometry and source metadata; 5/5 package checks. A new geometry test caught a USS specificity error in the first attempt; the final run passes after correction. Native dark Sound System captures cover Diagnostics at 1200/360 and actual Events source metadata at 620, with audio muted. Native Diagnostics was inspected in its honest runtime-unavailable state; populated runtime capture is not newly certified by this pass. No new PlayMode suite, native player build or listening test. Runtime code remains byte-identical to preview.26.

## Captured context restoration (preview.28, 2026-11-17)

Rebuilding Diagnostics previously preserved its selected row but left the newly created detail labels empty until another selection. The visual tree now restores the selected capture immediately. Captured scope and actual/limit counts, cooldown remaining or voice capacity are presented separately, using only the stored rejection data. Current asset settings do not replace those values. Missing context stays explicitly unavailable; an accepted request does not imply measured audibility.

The isolated BAD SHIFT normal-Editor acceptance helper exercises real music requests/cancellation/crossfade, cooldown rejection and accepted zero-gain playback with editor audio muted and AudioListener volume zero. It checks selected detail after rebuilding, wide/narrow placement with populated records and real captured cooldown context. This is runtime UI assertion evidence, not a listening test or a native player build. The helper and reports are archived in the development workspace and excluded from the delivered project. Fresh totals are recorded in DARK-UI-VERIFICATION.json.

## Diagnostics project library (preview.29, 2026-11-17)

Diagnostics now follows the reference three-column composition with a searchable library of actual project SoundEvent assets, captured requests in the center and captured details on the right. Selecting a library event filters requests by the requested asset identity and selects that asset in the native Inspector. It does not edit an asset or trigger playback. Child rejection settings navigation still points at the actual problem asset; the requested-event filter refers to the request root. Same-named assets remain distinct.

Below 1100 px the library hides, while the explicit Requested event field and All requests action remain available. Below 900 px captured detail stacks beneath the overview. Text/rejection filters continue to combine with the event filter. Project search does not fabricate records for assets that have not played.

Fresh verification: 323/323 EditMode and 5/5 package tests, plus 28/28 muted normal-Editor BAD SHIFT runtime assertions. The runtime probe includes real requests, identity filtering, rebuild preservation and restoring all requests. Native Sound System screenshots show the real project library at 1200 px and selected-event scope at 360 px; an actual mouse selection was confirmed in native Unity Selection. Populated fixture evidence remains runtime UI assertions, not screenshots. No new standalone PlayMode suite, native player build or listening test. Runtime bytes are unchanged from preview.28.

## Events overview and context (preview.30, 2026-11-17)

Events now separates the source overview from the right-hand asset context in wide windows. Native Inspector actions and pin/follow, actual routing/parameter references, runtime audition and authoring diagnostics occupy the graphite context column. The center keeps source rows, waveform, measured metadata and Play source (dry). Below 1100 px the same context element moves beneath sources; resizing does not recreate audition controls.

The reference draws an Inspector inside its composition, but the actual project contract keeps authoring in Unity's native Inspector. This pass therefore labels the embedded column Asset context and keeps its data read-only rather than duplicating editable serialized fields. Show settings and Pin Inspector retain their existing native behavior. Full visual parity with a docked native Inspector remains a layout/acceptance item.

Fresh EditMode validation covers selected source, native selection, unchanged serialized assets and audition-control identity across 1200/620 px. Native dark Sound System screenshots show actual SFX_Impact source data and honest unavailable runtime audition at those widths, with output muted. Test totals and packaging parity are recorded in DARK-UI-VERIFICATION.json. No fresh runtime probe, standalone PlayMode suite, native gameplay build or listening test is claimed for this composition-only pass. Runtime code remains identical to preview.29.

## Native Inspector readability audit (preview.31, 2026-11-17)

A native 300 px Inspector review found mixer output and concurrency group fields truncated to half the available width. Narrow object-reference fields now stack the label above the full-width selector; native serialized binding, object picker, Undo and multi-edit remain unchanged. The audit also found collapsed summaries still visible on sections restored as expanded. Section layout now synchronizes summary visibility with the actual foldout state, including value restoration without a change event.

Fresh evidence: 325/325 EditMode tests, including object-field bounds and silent foldout restoration; 5/5 package tests. Native Sound System screenshots show the corrected 300 px Inspector, expanded playback limits and a 360 px empty Review changes window. The existing pending-proposal test ran freshly in BAD SHIFT and checked narrow/wide reflow without importing or applying its proposal. The native Review screenshot is an empty state, not pending-plan proof. No new runtime probe, standalone PlayMode suite, native build or listening test. Runtime is byte-identical to preview.30.

## Weighted and Switch Inspector audit (preview.32, 2026-11-17)

Narrow weighted-source fields now give source-slot names the full column width and wrap long names above the numeric input. This keeps the source-to-weight relationship visible without relying on a tooltip. Missing weights still use 1; merely displaying or resizing the Inspector does not materialize those defaults in the asset.

Fresh 326/326 EditMode checks include long source names at 300 px and unchanged absent weight arrays; 5/5 package tests pass. Native 300 px weighted and expanded Switch child Inspectors were inspected silently using a clearly named transient QA copy referencing existing source content. The copy was destroyed after inspection; no source asset or scene was changed. Screenshots identify the QA copy and are not evidence of new shipping demo content. Existing child references, state and gain controls were readable. No fresh runtime/PlayMode suite, native build or listening check; runtime bytes remain identical to preview.31.

## Music profile Inspector (preview.33, 2026-11-17)

The native Music State Profile Inspector now uses the shared graphite surface and divided States, Default transition, Transition rules and Runtime preview sections. Counts come from the actual asset. Default timing explicitly applies when no matching route exists; validation and existing native array editing remain available. Fields stack below 380 px. Opening the Music workspace is distinguished from starting playback. No runtime, serialization, timing range or transition semantics changed.

Fresh 327/327 EditMode tests include a 300 px music profile Inspector, honest empty validation, unchanged JSON on opening, native serialized editing and Undo restoration. All 5 package checks passed. The native Sound System 300 px screenshot uses a clearly named temporary empty QA profile, removed afterwards; no shipping profile was invented. No populated music Inspector screenshot, fresh runtime probe, PlayMode suite, native build or listening check is claimed. Runtime remains byte-identical to preview.32. Music beat timeline and live layer meters remain separate backend/UI work.

## Populated music routes (preview.34, 2026-11-17)

Transition-rule headings now show the actual source → destination pair, including when folded. Labels match the default timing controls: From state, To state, Boundary, Crossfade (s), Curve. Native serialized properties retain validation attributes, reorder behavior and Undo; an IMGUI fallback preserves those controls. No timing, runtime or serialized-field changes.

A real native Inspector audit found Unity's inline label alignment overriding narrow USS rules. Narrow music Inspectors now suspend aligned field layout and restore it when widened. This avoids mid-word labels and excess whitespace in nested rules. Long route headings wrap within the available width.

Fresh final validation: 328/328 EditMode tests (review34-verified-edit.xml), including a real pinned native Inspector at 300/520 px, unchanged serialized data on display/resize, destination edits and Undo with title refresh. All 5 package tests pass. A prior intermediate build had a StyleLength overload compiler error; fixed before the passing final run. Native screenshots at 300 and 520 px use a transient QA profile with an actual demo track and an intentionally missing second track; the visible validation is genuine. The QA profile was removed. No scene or real import plan changed. No fresh runtime probe, PlayMode suite, native gameplay build or listening check.

## Mixer routing profile authoring (preview.35, 2026-11-17)

MixerRoutingProfile now uses a native multi-edit Inspector with Target mixer, Parameter bindings and Exposed parameter checks sections, matching the graphite context styling. Serialized fields, native arrays, curves and mixed values remain bound through Unity. An explicit action opens the assigned native AudioMixer; it is disabled without a mixer or for multiple selected profiles. Expected names are documented as extra checks, not parameter bindings. Output units belong to the target effect (volume dB, frequency Hz, etc.); the Inspector does not pretend every exposed value is a gain.

Diagnostics read the assigned mixer and report missing assignments, invalid bindings and absent exposed names. They never call SetFloat or register the profile at runtime. Manual Check exposed names refreshes after changes made in the native mixer. No runtime-ready claim, topology or live meter is inferred from a successful check. The shared responsive helper now serves music and mixer profiles without changing serialization.

Fresh 329/329 EditMode tests (review35-edit.xml) include mixed expected names, unchanged assets on opening, multi-object serialized editing and Undo restoring both original values. The existing native music route tests ran again after sharing the layout helper. All 5 package tests pass. Native Sound System QA screenshots show 300/520 px and a genuine missing exposed name followed by an existing name that clears the warning. The temporary profile referenced the real template mixer and existing parameter; only the QA profile was edited, then removed. No mixer values, scene or real import plans changed. No new runtime probe, PlayMode suite, native gameplay build or listening test. Full routing topology remains unavailable.
