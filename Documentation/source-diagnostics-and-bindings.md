# Diagnose a scene source

In Play Mode select the emitter in the Hierarchy. Open Diagnostics and expand **Live source · Why is it silent?**. The Event Editor Profiler also opens this panel. Turn off **Follow selection** to inspect one object while moving the listener. Multiple attached physical voices can be inspected individually.

For a playing voice the panel reads its clip, output group, distance, spatial blend, attenuation limits, volume multipliers, current low-pass and occlusion. The displayed linear distance gain is an estimate. The source and full listener mix have separate sampled peaks in dBFS. A silent buffer is a snapshot. Listening, spatializer output and the individual voice's contribution after mixer effects require separate verification. Position-only one-shots can be investigated in captured events.

# Bind an existing event from chat

Enable the local agent bridge in Edit Mode and open the saved scene. For example, ask “Find the fridge in the current scene and propose using FridgeCompressor for its emitter.” Resolve ambiguous objects and SoundEvents before proposing.

1. Call audiomw_inspect with objectQuery. Use the returned scene path, object Id and Hash plus the GUID of a saved SoundEvent from inventory. Search matches up to 32 names in the active scene.
2. Call audiomw_propose_binding with requestId, intent, objectId, expectedHash and eventGuid. A missing SoundEmitter is added with its normal defaults. An existing single emitter retains its settings and changes its event. The reviewed event supplies routing and spatial settings.
3. Open the proposal using audiomw_show_review. The Unity user inspects the exact object and before/after state, approves or rejects it, and chooses when to save the scene. Application is one Undo step. Reinspect after scene or asset changes.

For a new audio file, first propose the file import and wait for human review. Then inspect the newly created SoundEvent and propose the binding. Gameplay triggers beyond the SoundEmitter lifecycle require their own implementation.
