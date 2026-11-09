# BAD SHIFT — interactive demo, 9 November 2026

Implemented in the isolated AudioMW-BadShift project. Original Relay Field and
AudioMW middleware source remain unchanged.

## Controls

- WASD: movement relative to camera heading. Shift: run. Space: short jump.
- Right mouse drag: orbit. Mouse wheel or +/- buttons: zoom.
- C / Follow: focus on the player. Home: restore overview. Arrow buttons rotate.
- Walk through the three glowing bananas to collect them. E collects the four
  remaining ingredients. The exit requires 7/7.
- Walk into chairs to push them; sprint into a dining table to knock cups off it.
- V: manager VO; X also speaks when near him. Entering his zone greets once, with
  a 15-second retrigger interval. The speech bubble uses the actual active subtitle.
- Reset scene (top right), or R: restore furniture, cups, pickups, camera, audio
  state and pause. Shuffle bags also restart. P pauses gameplay and physics.

## Physical and audio behavior

The player uses CharacterController, earth gravity and an analytically integrated
0.55 m jump. Ground acceleration is 28 m/s² and braking 40 m/s². Ceiling contacts
cancel upward velocity. Collision pushes transfer bounded impulses to dynamic
bodies: 120 N walking, 260 N running, with target prop speeds 1.6 / 3 m/s.

Tables are 18 kg compound rigidbodies; chairs 4 kg; cartons 0.2 kg; cups 0.35 kg.
Cup collision uses a convex hull with a flat base. A normal impact speed of 2.2 m/s
breaks a cup once into six physical shards, removed after eight seconds. Four cups
bound this demo to 24 simultaneous shards. Sibling shards ignore one another.

PropImpact and CeramicBreak are separate spatial AudioMW SoundEvents with the
existing mixer routing. Their clips are still synthetic placeholders. Gentle
settling below 1.2 m/s does not trigger impact audio. Push sounds are rate-limited.
The pickup event and music stinger use AudioMW as before. All real sound design
and human listening approval remain pending.

Camera movement never changes listener position: the listener remains on the
player. Listener orientation follows the view for consistent stereo presentation.
Zoom is orthographic and does not change sound attenuation. Move the player to
compare refrigerator distances; camera orbit changes viewing/stereo orientation.

## Reproduce

Enable com.unity.modules.particlesystem 1.0.0 in the isolated project's manifest.
After the prior reference/furniture authoring, run BuildBadShiftInteractions.Run
once. The existing prepared scene already includes it. StabilizeCups is an
idempotent migration for the initial interaction fixture, not needed on fresh Run.
FitBadShiftAprons.Run refits clothing for close camera views and builds the player (repeatable).
Normal builds use BuildAudioPlayground.BuildPlayer; do not rerun scene authoring
over hand edits.

## Validation

210/210 PlayMode tests in Unity 6000.3.21f1, including 23 demo/integration tests.
Actual controller jumps, chair pushes, table movement and cup spills, falling-cup
fracture/event dispatch, trigger-based triple collection, orbit/zoom listener
invariance, active VO quotes, and scene reset are covered. The tests exercise the
same movement method used by keyboard input; native keyboard/UI checks are separate.
No Unity 2022 compatibility or human listening sign-off is claimed.

Native checks confirmed camera focus/rotation, all three banana pickups without E,
VO speech bubble, keyboard jump and Reset scene returning to 0/7 and the overview.
Buttons use dark text; the quote balloon is rounded with a tail.
