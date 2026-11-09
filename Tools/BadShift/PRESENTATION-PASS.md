# BAD SHIFT presentation and guided demo

The background is muted blue-gray; pastel scene materials remain readable under softer
key lighting. The overlay uses flat rounded controls, 24-unit outer margins,
translucent cards and a reference canvas of 1600 by 900. The canvas scales with the
window. The header contains manual play, guided replay and scene reset. The lower
cards separate explanation from live station readings and camera controls.

## Two modes

Play yourself returns movement to WASD. Watch demo reloads the authored scene and
starts a guided run with real CharacterController movement, collisions, pickups,
footsteps and AudioMW playback. Six stages cover surface/pickup events, quantized
music, Random/ShuffleBag, voice ducking, Dry/Echo/Reverb and distance attenuation.
Captions explain the mechanism; live readings come from the actual runtime.
The tour returns control after completion. A blocked route stops with a visible
message rather than teleporting through furniture. Simulation continues without a
keyboard device; the tour does not depend on keyboard presence.

All narration is currently written on screen. The manager's VO remains synthetic
placeholder audio; no recorded spoken tour or listening sign-off is claimed.

## Unity and standalone

Open Assets/Playground/RelayField.unity in the isolated AudioMW-BadShift project.
Tools > AudioMW > BAD SHIFT Demo provides Watch demo in Game View and Play yourself.
The launcher starts Unity Play Mode and uses the same runtime tour as the build.
Game View should use Free Aspect when resizing. The launcher refuses other projects
without the .bad-shift-demo marker and preserves a dirty unrelated open scene.

Standalone builds enable PlayerSettings.resizableWindow and Windowed mode, with a
1280 by 720 default. Drag a window edge or use macOS window zoom. Existing saved
window dimensions may override the default, but resizing stays enabled.
Unity API reference: https://docs.unity.com/en-us/engine/6000.3/script-reference/unityeditor/playersettings/resizablewindow

## Reproduce and check

Copy the runtime, editor and test snapshots into the prepared isolated project.
Run BuildBadShiftPresentation.Run to install the tour and presentation settings;
then run PlayMode tests and BuildAudioPlayground.BuildPlayer. The authoring pass
is repeatable and guarded by the isolated-project marker.

Three integration tests cover full autonomous traversal with real movement and
recorded playback, manual takeover clearing movement, and replay reloading the
scene before restarting the tour. Verification totals are recorded in the
prepared project's BAD-SHIFT-VERIFICATION.json after the current run completes.

Current automated result: 218/218 PlayMode tests passed in Unity 6000.3.21f1, including 31 demo/integration tests. Demo fixtures unload RelayField before subsequent middleware tests, preventing scene Update methods from modifying runtime state during unrelated tests.

## Physical props and room layout

The tomato ingredient is a light rigidbody with a bouncy material. Walking into it
applies a bounded kick with a small upward impulse; contact sounds use AudioMW.
It can still be collected with E. The picnic table, benches and parasol form one
compound rigidbody. The low tomato crate tips independently and contains four
separate loose tomatoes. Other food pickups, the carrot crate and shelf jars are
also dynamic. Appliances, architecture and the manager's desk stay fixed.

E toggles the stockroom door within reach. A kinematic hinge animates the actual
collider, stopping its sweep when the player or a dynamic prop blocks it. The
guided tour opens this door before entering. A nearby UI prompt names the action.
The dancer and smaller stage have clearance from the room wall and kitchen cabinet;
the cabinet and microwave move together. The cafeteria text is centered on its sign.

The demo uses Active Input Handling = Both: the existing IMGUI overlay requires
legacy mouse events, while gameplay uses the Input System. This is a demo project
setting, not an AudioMW package requirement. Unity documents this IMGUI boundary:
https://docs.unity3d.com/Packages/com.unity.inputsystem@1.14/manual/UISupport.html

Run the prop authoring methods in this order on the prepared pre-props scene:
BuildBadShiftProps.Run, LooseProps, SettleAndInspect, ClearExit, SupportShelf. Run and ClearExit
are one-time layout changes; do not repeat them on the final authored project.
ClearExit separates the goal frame from the refrigerator and relocates the smaller
cheese pickup away from circulation. SettleAndInspect places food on the floor
using collider bounds instead of leaving it suspended at its former pickup height.

The voice-limit integration check asserts exactly two admitted stress voices and
zero remaining stress voices after their timer. It does not treat independent prop
collision sounds as leaked stress voices. Physics checks also cover kick height,
peak tipping angle, door open/close, blocked door motion, and untouched-prop drift.

Native review confirmed the compact translucent overlay, blue-gray background, centered sign and separated stage/cabinet. The Unity launcher started the guided tour visibly in Game View. Standalone mouse-button automation generated no input events in the player. On 2026-11-09 the user confirmed that clicking Watch demo in the new standalone build starts the character automatically. This verifies the Watch demo button with live user input; it does not imply a full manual walkthrough or listening sign-off. Shelf jars have a solid support collider and an explicit support-height regression assertion.
