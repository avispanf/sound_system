# Editor UI guide — first native stage

2026-11-10. Reference: audiomw-editor-ui/START-HERE.txt, UX-SPEC.txt and NAMING-AND-UX-REVIEW.txt v1.1, then screens/unity-sounds.png; art/unity-shell.png supplies visual direction only. AudioMW remains the product/namespace. Implemented against main in Sound System; unrelated EditorSettings change is preserved. No authored scenes, sound assets or mixer values are rewritten.

## Existing contracts retained

Design 02 already provides read-only Browser, ordinary native selection, explicit pin/follow, source selection/preview lifetime, native serialized Inspector with Undo/multi-edit, dB/legacy gain semantics, full Blend curves and child tables. Music Current/Pending and captured transitions, Diagnostics Event/Outcome/Reason and contextual settings navigation, audits, Live Tweaks and Agent Review remain available. These tools are not collapsed into one window or replaced by mock content.

## First stage

- Events labels the Browser tab/navigation; existing Sounds menu and public method remain compatible. Parameters opens the actual parameter library filter. Routing opens existing Mix Setup, whose confirmation contract is unchanged. A separate Review changes toolbar button opens Agent Review without applying a plan. All previous Tools entries remain.
- Read-only overview has a compact context heading and a larger wrapping asset title. A restrained teal accent and measured waveform follow the guide. Native theme variables preserve light/dark ownership; no screenshot assets or mock values are embedded.
- Gain adds an explicit Mute action through the existing serialized binding; multi-edit and Undo retain linear values. Reset remains 0 dB.
- Simple displays Random / Sequence in the native enum field. RandomNoRepeat displays No immediate repeat. Serialized enum IDs, field names and APIs stay intact. The Inspector states that selection history is shared per event and Sequential advances per trigger. Switch states Evaluated at trigger.
- Play source (dry) keeps a permanent no-processing explanation. Failed PCM reads show Unavailable with a reason rather than silent LUFS. Addressed assets still do not trigger bank loading by browsing.
- The overview links to the actual mixer group and native mixer, or states no group is assigned. Parent containers explain leaf routing. Actual parameter references are selectable; no global scope, live parameter value or invented mixer hierarchy is asserted.
- Play event (runtime) requires Play Mode, an existing AudioRuntime, an explicitly chosen active scene SoundEmitter already using that event, exactly one active AudioListener and unpaused playback. The pane displays scene/listener and reasons for unavailability. It calls the existing emitter Play, preserving local parameters, attachment behavior, routing and shared selection history. Selecting an emitter never assigns or modifies it. Requests can still be rejected; Diagnostics owns the actual outcome. There is no new authoring simulation engine.

## Remaining scope and backend needs

P1: dedicated Spatial workspace, full parameter/reference workspace, deeper routing graph, child inheritance overview and contextual onboarding. Existing native fields, parameter library, child editors, Mix Setup and mixer editor remain the owners of their data. A safe Edit Mode processed audition would need a separately designed runtime context and lifetime; dry preview does not provide it. Live output LUFS/CPU/voice timeline require measurement/capture infrastructure. No mock meter substitutes for this.

Unreal remains concept only: a separate plugin, asset/transaction model, Details editors, emitter/listener and playback adapters, units conversion, scheduler validation, content import and human-review bridge are required. The Unity backend is not an Unreal binary plugin. Start with one event/import/assign/play/diagnose vertical slice before adaptive music parity.

## Verification

Fresh test counts, native screenshots and explicit limitations are recorded in the delivered project's EDITOR-UI-VERIFICATION.json. Prior design QA and old test runs are not evidence for this stage. Listening is excluded: development and visual checks are muted. Full native light/dark, narrow Inspector, keyboard, missing-reference and multi-edit acceptance must be distinguished from automated coverage.
