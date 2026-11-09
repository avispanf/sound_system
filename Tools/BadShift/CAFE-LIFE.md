# Cafe life pass

The sparring flow below describes the first implementation. The current prepared
scene uses BATTLE-ARENA.md: E opens an intro, HP bars appear, and victory/defeat wait
for an explicit rematch or reset. The former automatic respawn is superseded.

## Play

E selects the closest reachable target within 2.3 metres; the HUD names the action.
A nearby collectible still takes priority inside one metre.

- Microwave: E plays its AudioMW container and briefly brightens the coral housing. Q still changes Random / ShuffleBag; X still auditions the station.
- Broccoli desk: E queues the existing VO and its subtitles. The boss waves, turns, blinks and jiggles its crown. Impacts and broken cups trigger startled gestures and short comic speech bubbles. Actual VO subtitles take priority over reaction text.
- Bathroom: E opens/closes the door, including when the player stands against it.
- Fridge: E opens/closes the lower compartment; G independently toggles the upper compartment.
- Cups and loose food: E tosses them. Furniture and crates: E pushes them. Walking/running collisions remain physical; six cups can shatter.
- Sink and toilet cistern: E auditions the existing room-effect sound and a joke. These are sound demonstrations, not water or flushing simulations.
- Employee on the purple pad: E starts breadstick sparring and attacks again. Both characters show swings and impact flinches; health appears in the lower-left HUD.
- Sound on/off: mutes the global listener without pausing music clocks, motion or the tour. Reset restores the initial sound state.

## Sparring

Player and rival start with 100 health. Each E strike deals 25 damage, with a 0.55-second cooldown and a 0.35-second arm gesture. The rival counters after 0.8 seconds, then every 1.1 seconds for 30 damage. Strikes require range and an unobstructed line between actors. Leaving the 2.7-metre sparring radius ends the exchange and hides the sticks; health persists until a defeat or scene reset.

At zero health the skeleton falls into a comic side pose and cannot attack. A defeated player cannot walk or jump. After three seconds both health values reset; a defeated player returns to the original spawn. There is no ragdoll or dismemberment. Pause stops timers; VO suspends attacks and staff movement.

## Door contact

Interactive doors sweep in increments no larger than two degrees. The interacting character is moved away from a penetrating leaf through CharacterController.Move and then checked again. If a wall prevents clearance, or a dynamic prop obstructs the sweep, the door stops. Programmatic opening still treats a character as an obstruction; no collider is disabled to force passage.

## Verification

Unity 6000.3.21f1 PlayMode: 237/237 passed, including seven new cafe-life tests and the existing reset, replay, bank ownership, gait and audio tests. The new tests cover microwave playback/highlight, bathroom and both fridge doors at contact distance, broken-cup reactions, rival defeat/reset, player death/respawn/movement recovery and mute without pause.

Post-pose Unity renders inspected for sparring, player defeat, the broccoli, bathroom and open fridge. All music, voice and effects are still placeholder audio; no human listening approval is claimed. The art remains an approximation of the reference.

## Quick manual route

Select Play yourself. Approach the microwave and press E; walk up to the fridge and try E then G while touching its front. Return to the bathroom door and use E. Toss a cup near a table to see breakage reactions, then approach the employee and repeatedly press E to win a round. In the next round attack once and wait for the rival to knock you down; after three seconds check that movement returns at spawn. Toggle Sound off/on and use Reset to restore the whole cafe.

## Reproduction

Copy the runtime, editor and test sources to the corresponding folders in the isolated prepared project. After the previous art/performance passes, run CafeLifePass.Run once, then BuildAudioPlayground.BuildPlayer. The authoring method requires .bad-shift-demo and refuses a scene already containing BadShiftCafeLife. Normal iteration uses the authored scene rather than re-running authoring.
