# Breadstick Warden encounter

The employee and stage move to the open front-right kitchen floor at (7, -5) in X/Z. The old collision object near the bathroom is removed. The opponent now has a CharacterController that follows its visible body.

## Flow and controls

Approach the dancing employee. The invitation appears only when the employee is the nearest E target. E starts a 1.2-second introduction without dealing damage. In the fight, left click attacks, holding right click blocks, WASD moves and Shift runs. Middle-mouse drag orbits the camera during battle; right-mouse orbit remains available outside battle. Clicks on the top HUD and bottom control strip cannot trigger attacks.

Both actors start with 100 HP. Player swings last 0.35 seconds with a 0.65-second cooldown and deal 25 damage. The rival prepares a visible 0.7-second windup and deals 20 damage. The actual animated stick is swept through space using capsule queries; a hit requires contact with the opponent controller, a clear path and a valid facing/height. One swing can connect only once. The rival pursues inside the arena instead of chasing through the whole cafe.

A frontal block prevents damage and plays a separate clack with a blue pulse and small recoil. The guard slows movement and cannot be raised during an active player swing. Arm IK keeps the hand attached to the visible stick during attacks and guarding. The normal combat stance faces the opponent.

Hits apply a horizontal impulse, body flinch, warm colour pulse and light accent. Player knockback shares the same CharacterController.Move as gravity and locomotion; enemy movement also uses one controller move per frame. Controllers preserve collision and grounded state, including at very high frame rates.

At zero HP, control and attacks stop. The defeated skeleton falls over for 0.85 seconds, then settles. The result overlay appears after 1.15 seconds. Only then do background music and musical ambience become silent and the outcome theme start. Victory and defeat remain until Fight again or Reset demo. Fight again restores HP and starting positions; Reset demo restores the entire cafe. Play yourself exits the fight and restores normal presentation. Pause stops timers; Sound on/off remains global.

The opponent displays short timed taunts, including instructions from Mr Broccoli, cutlery bravado and reactions to hits or blocks. These are text captions; recorded spoken voice-over is still pending.

## Presentation

The battle uses a separate global Volume on layer 0, priority 20, blended by weight: exposure -0.3, contrast 18, saturation -12, vignette 0.32 and bloom 0.65. Red and cobalt arena lights, a floor ring and low translucent smoke accompany the darker key light and background. Normal scene lighting returns on exit. Character colours use renderer property blocks; authored materials are not modified at runtime.

Two HP bars and a contextual control strip replace the normal lower HUD during combat. Intro, victory and defeat have distinct overlays. The rest of the demo stays available through the top controls.

## Audio

Six routed AudioMW SoundEvents are added to the existing bank: Battle_Challenge, Battle_Attack, Battle_Hit, Battle_Block, Battle_Victory and Battle_Defeat. Attacks, hits and blocks are spatial. The two non-spatial result themes route through the Music mixer group independently of the muted background MusicPlayer. Victory lasts 4.8 seconds; defeat lasts 4.5 seconds with descending notes. These are distinct original synthesized prototypes, ready for replacement by final sound design. Rematch stops the previous result theme before restoring the normal music.

## Reproduction

Use the isolated prepared project with .bad-shift-demo. After CafeLifePass.Run, copy the updated Runtime assembly definition (it references URP/Core Runtime), runtime scripts, editor scripts and tests. Run Audio/generate_battle_audio.py with the project path, then BattleArenaPass.Run once. The author checks the project marker, URP, HDR and camera post-processing, and refuses an already-authored battle scene. For ordinary iteration, keep the saved scene and run BuildAudioPlayground.BuildPlayer.


## Verification

Unity 6000.3.21f1: 247/247 PlayMode tests passed, including 17 battle/cafe/melee cases. Coverage includes the explicit challenge, pointer attack versus block, protected HUD clicks, visible-weapon contact, cooldown, knockback, grounded-state preservation, dodge timing, wall obstruction, pause, persistent outcomes, delayed fall-to-result transition, background music silencing, rematch and normal-lighting restoration. Previous audio, tour, door, locomotion and reset tests also passed.

Post-pose Unity renders show the stick contacting the opponent and the completed fall. Audio clips are synthesized prototypes; no human listening sign-off or mobile qualification is claimed.
