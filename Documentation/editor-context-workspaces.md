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

## Pending Review visibility (preview.36, 2026-11-17)

Event routing is now visible alongside before/after information instead of hidden under Why. Both event and music proposals summarize captured channel policy (keep source layout / force mono); event file details now carry the same explicit channel-policy explanation as music files. These are importer settings from the plan, not measured source channel counts or a claim of stereo playback. Review context shows the captured build target and Unity version. Approval remains whole-plan with human confirmation; no partial selection or fake reference data added.

Fresh 329/329 EditMode tests (review36-edit.xml) include expanded assertions for visible event routing/channel policy, file details, fixed confirmation footer and absence of imports. All 5 package checks passed. Native Sound System captures at 1200/360 px show a clearly marked QA-only pending proposal prepared from an existing demo source and target event. The proposal was never approved or applied; its destination folder remained absent. The bridge began disconnected with zero plans and was restored disconnected after the QA plan was expired/cleared. No user plans existed or were changed. The isolated BAD SHIFT fixture provided the fresh tests and package import validation. No new runtime probe, PlayMode suite, native gameplay build or listening test.

## Mixer binding identity (preview.37, 2026-11-17)

Mixer parameter bindings now show source parameter → exposed name even when collapsed, instead of Element 0. Mixed and unassigned references are explicit; labels identify sound parameter, response curve and output bounds without inventing output units. Fields remain native serialized PropertyFields, with an IMGUI fallback. Runtime mapping and ranges are unchanged.

Fresh final 330/330 EditMode tests (review37-final-edit.xml) cover real pinned 300 px Inspector bindings, unchanged serialization on opening, target editing/Undo, title refresh and mixed-target headings. The first run exposed timing-sensitive title assertions in both music and routing tests: Undo data restores before UI Toolkit's tracked label refresh. Tests now check restored data separately and wait up to 3 seconds for the correct tracked label. No production delay/workaround was introduced. All 5 package checks pass. Native Sound System screenshots show expanded/collapsed 300 px QA routing bindings with actual parameter/mixer references; the temporary profile was removed, no mixer/scene changes. No new runtime probe, PlayMode suite, native gameplay build or listening check.

## Music composition and evidence boundaries (preview.38, 2026-11-17)

Music state buttons now show state names, above Current/Pending status, matching the reference hierarchy. The context column lists each destination's current profile rule; tooltips retain the track and timing. On narrow windows the same context moves beneath the overview. Captured pending timing stays distinct from editable profile rules. No fake timeline, layer meter or sampled waveform added.

Fresh 330/330 EditMode tests (review38-edit.xml) verify compact labels, order, profile-state rename refresh and narrow context reflow. All 5 package tests pass. Fresh normal Unity Editor runtime probe on the isolated BAD SHIFT fixture passed 30/30 assertions (music-runtime-acceptance38-final.txt): real scheduling, repeated-click boundary preservation, compact Combat label, rule context showing an edited 9-second fade while the queued transition retains its captured 2 seconds, cancellation, DSP crossfade completion, actual diagnostics and narrow/wide geometry. The Editor and AudioListener were muted. Native main Sound System 1200/360 screenshots use a transient QA profile with an existing track outside Play Mode; they are not screenshots of the runtime probe. QA objects/helper were removed. No native player build or listening check.

### Reference acceptance status at this checkpoint

| Surface | Implemented | Remaining boundary |
| --- | --- | --- |
| Events | Library, sources, actual waveform/metadata, right context, separate source/runtime audition | Embedded context is read-only; native Inspector remains separate rather than duplicated inside the window |
| Native Inspector | Event container labels, narrow controls, music and routing sections, route/binding headings | Full visual match depends on Unity docking; no new artwork assets or custom native editor shell |
| Music | Profile library, compact states, Current/Pending, captured timing, visible profile rules | Reference beat timeline and live layer meters are not implemented |
| Spatial | Actual distance-response graph, authored settings and context | Custom curves without authored data remain unavailable |
| Parameters | Authored references, response information and global runtime scope | Dynamic code references and per-instance overrides are not exhaustively inventoried |
| Routing | Actual output assignments and binding authoring, native mixer navigation | Parent topology, sends, snapshots and bus meters remain unavailable here |
| Diagnostics | Event library, captured requests, selected details and reasons | Captured state is historical; it does not certify audibility |
| Review | Proposal library, actual changes, context, visible route/channel policy and fixed human confirmation | Approval applies the whole plan; partial-selection mock controls intentionally absent |

This is a scope checklist based on current source plus the dated evidence above, not a claim that every window was freshly retested in this block. Earlier screenshots/tests retain their original dates. Unreal remains a future native plugin design, not a shipped implementation.

## Sound parameter Inspector (preview.39, 2026-11-17)

SoundParameter now uses the shared dark native Inspector layout with Value range, Default value and Authored scope sections. Stored default remains editable through serialized binding; effective default shows the runtime clamped value separately. Invalid bounds/defaults and constant single-object ranges are explained without auto-correction. The global flag is described as authored scope, not a mechanism that moves values between stores. No smoothing control is invented: this asset does not own smoothing. A button opens the selected persistent parameter in the read-only Parameters workspace.

Fresh final332/332 EditMode (review39-final-edit.xml) checks a real pinned300px Inspector with an out-of-range stored default, unchanged JSON on display, editing/Undo, mixed defaults and multi-object Undo restoring different values. All5 package checks pass. Native SoundSystem300px screenshot uses actual PARAM_Intensity without editing; a live mouse click opens Parameters on that asset and its actual three music-layer references. Main Console0errors; output muted. No new runtime probe, standalone PlayMode suite, native gameplay build or listening test. Runtime bytes unchanged frompreview38.

## Music track authoring (preview.40, 2026-11-17)

MusicTrack now uses the shared dark responsive native Inspector. Sources contains intro/body/loop and actual clip-derived duration; Musical grid contains BPM, beats per bar and native markers; Vertical layers retains the native layer array; Output exposes the mixer group and existing serialized decibel gain field. Marker indexing is explicitly zero-based and grid scheduling is distinguished from time stretching. Layers are authored settings, not live meters. No field, range, serialization or transition behavior changed. Multi-edit remains supported.

Fresh334/334 EditMode (review40-edit.xml) covers actual pinned300px Inspector source identity, unchanged JSON on opening, gain-control bounds and gain edit/Undo. A second test checks mixed gain editing and Undo while preserving distinct tempos. All5 package checks pass. Native main SoundSystem screenshots show actual MUS_DemoTrack at300px, including source/grid/layer and output/gain sections; only foldout view state changed, no authored asset changes. Muted, Console0errors. No new runtime probe, standalone PlayMode suite, native gameplay build or listening test. Runtime bytes identical topreview39.

## Attenuation preset authoring (preview.41, 2026-11-17)

AttenuationPreset now uses the shared dark responsive native Inspector. Distance response separates stored near/far values from applied getter limits; spatial settings retain native range controls for blend, spread and Doppler. Units are Unity world units, with the preview's metre assumption stated explicitly. Custom response stays visible but inactive for non-custom modes; its serialized keys are preserved. Mixed rolloff selection leaves the curve editable. A native action opens the selected persistent preset in Spatial. No AudioSource behavior, curve normalization, field ranges or serialized values were changed.

Fresh335/335 EditMode (review41-edit.xml) includes a pinned300px Inspector with stored negative distances, applied0.01 limits, unchanged JSON/inactive curve and distance editing/Undo. All5 package checks passed. Native SoundSystem actual ATT_SmallRoom inspected at300px, then live mouse navigation opened its existing Spatial model. Screenshots show actual asset data; no authored changes. An initially blank floating panel was resolved by selecting it through Window > Panels; no source workaround added. Console0errors, muted. No new runtime probe, standalone PlayMode suite, native gameplay build or listening check; runtime bytes identical topreview40.


## Music layer identity (preview.42, 2026-11-17)

Music layer rows now expose the authored name and controlling parameter without expanding an anonymous array element. Missing parameters show Fixed weight; mixed layer names/parameters are explicit. The expanded drawer keeps all six serialized fields native, including the existing weight range and curve. Fallback weight is labelled as such because it also applies when a parameter is assigned but its curve is empty. No layer evaluation, fades, defaults, clip selection or runtime APIs changed; UI Toolkit and IMGUI paths remain available.

Fresh336/336 EditMode (review42-edit.xml, 06:08:40–06:09:33UTC) includes pinned300px label geometry, unchanged JSON on opening, authored rename/Undo and parameter removal updating the collapsed title. All5 package checks passed. Native Sound System screenshots show actual MUS_DemoTrack layers collapsed/expanded at300px; only foldouts and scroll changed. Console0errors; muted. No new runtime probe, PlayMode suite, native gameplay build or listening check. Reference Music timeline/live meters remain unimplemented.


## Long music layer headings (preview.43, 2026-11-17)

Music layer headings now wrap at narrow widths with the same flexible toggle layout as transition rules and mixer bindings. Authored long names and parameter identity remain visible without changing list selection, reorder controls or serialized data. Native300px screenshot uses a clearly named QA copy of the actual demo track; the copy was deleted afterwards. A blank duplicate native panel was closed after inspecting its contents; no production workaround was introduced.

Fresh final336/336 EditMode (review43-final-edit.xml, 06:40:45–06:41:04UTC) covers multi-line title height, containment inside the Inspector, native editing and Undo. An initial335/336 run compared the label with the inner Foldout boundary, which native label margins exceeded by2px; the final assertion uses the actual visible Inspector boundary. All5 package checks passed. Fresh normal Unity Editor BAD SHIFT runtime probe30/30 (music-runtime-acceptance43-final.txt) verifies real scheduling, repeated-request boundary preservation, current profile versus captured pending timing, cancellation, DSP crossfade completion, actual accepted/rejected requests, captured diagnostics and wide/narrow detail layouts. Both Editor and AudioListener muted. QA runtime helper archived outside Assets and excluded from delivery. Console0errors in main; no main scene/real-plan changes. No standalone PlayMode suite, native gameplay build or listening check. Music timeline/live meters remain unimplemented.


## Native pin identity (preview.44, 2026-11-17)

Pin now reuses windows by asset identity for AudioMW profiles as well as Sound Events. An immediate repeat reuses the tracked native window before its UI has bound. After a reload, owned profile roots identify their single target without relying on display names or private Unity fields. Closed/destroyed entries are discarded on the next request. Null targets return null; an unrelated focused PropertyEditor is no longer returned as a successful pin. Native selection is not reassigned. Profile root metadata does not affect serialized assets or multi-edit bindings.

Fresh337/337 EditMode (review44-edit.xml, 07:09:45–07:10:04UTC) covers immediate/after-binding reuse, two distinct same-name parameters, selection independence and null input, alongside existing Event pin/follow and authoring regressions. All5 package checks passed. Native Sound System repeated pin of actual PARAM_Intensity returned the same window and kept the total window count21→21; populated dark300px screenshot saved. No authored edits, scene/real-plan changes or sound. This fixes repeated profile pin ownership; the earlier initial blank duplicate panel is not claimed fully diagnosed. No fresh runtime probe, standalone PlayMode suite, native gameplay build or listening check; previous runtime evidence remains preview43.

API research: Unity's public [OpenPropertyEditor](https://github.com/Unity-Technologies/UnityCsReference/blob/master/Editor/Mono/EditorUtility.cs) delegates to native PropertyEditor creation. Implementation uses the public entry point and AudioMW-owned metadata; no private tracker reflection added.
