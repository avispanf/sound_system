# Breadstick Warden encounter

The employee and stage move to the open front-right kitchen floor at (7, -5) in X/Z. The old collision object near the bathroom is removed. The opponent now has a CharacterController that follows its visible body.

## Flow and controls

Approach the dancing employee. The invitation appears only when the employee is the nearest E target. E starts a 1.2-second introduction without dealing damage. In the fight, left click attacks, holding right click blocks, WASD moves and Shift runs. Middle-mouse drag orbits the camera during battle; right-mouse orbit remains available outside battle. Clicks on the top HUD and bottom control strip cannot trigger attacks.

Both actors start with 100 HP. Player swings last 0.35 seconds with a 0.65-second cooldown and deal 25 damage. The rival uses a two-hit sideways combination, an overhead chop and a straight thrust. Each hit has its own visible windup and deals 20 damage. The actual animated stick is swept through space using capsule queries; a hit requires contact with the opponent controller, a clear path and a valid facing/height. One swing can connect only once. The rival pursues inside the arena instead of chasing through the whole cafe.

A frontal block prevents damage and plays a separate clack with an expanding cyan guard pulse, a braced player recoil and a comic backward rebound on the attacker. The rival is stunned for 0.55 seconds and cannot begin another attack for at least 1.05 seconds, leaving a readable counterattack window. The guard slows movement and cannot be raised during an active player swing. Arm IK keeps the hand attached to the visible stick during attacks and guarding. The normal combat stance faces the opponent.

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

Unity 6000.3.21f1: 262/262 PlayMode tests passed, including 32 battle/cafe/melee/polish/outcome/combo/enemy-pattern cases. Coverage includes the explicit challenge, pointer attack versus block, protected HUD clicks, visible-weapon contact, cooldown, knockback, grounded-state preservation, dodge timing, wall obstruction, pause, persistent outcomes, delayed fall-to-result transition, background music silencing, rematch and normal-lighting restoration. Previous audio, tour, door, locomotion and reset tests also passed.

Post-pose Unity renders show the stick contacting the opponent and the completed fall. Audio clips are synthesized prototypes; no human listening sign-off or mobile qualification is claimed.

## Clay combat polish — 2026-11-10

Swings now have a short anticipation, accelerated cut, torso twist and a 0.18-second visual recovery within the existing attack cooldown. The head and free arm lag behind the torso. Weapon IK runs after the body reaction so the breadstick stays in the hand throughout the pose; the existing swept contact remains responsible for damage.

Holding RMB raises a two-handed guard with a braced torso, a cyan accent and a BLOCKING prompt. Blocked contact adds recoil and a blue pulse. Successful hits add a short warm radial accent and elastic body/head recoil. A small breadstick swish follows the visible tip; these accents reuse the existing material and do not add audio events or authoring dependencies.

Death includes an initial squash and backward hesitation, a faster sideways flop, loose arm/wrist follow-through, and a damped bounce. The visible body is supported above the controller's floor plane. Breathing stops on the defeated actor, and the pose settles before the existing delayed result panel. Rematch restores authored rig position, scale and rotation. Characters also take a short recovery step when knockback displaces their planted feet, preventing stretched legs after a hit.

Runtime changes work with the saved battle scene; no additional scene-authoring pass is required. The new pose tests sample after LateUpdate and cover both grips, one physical contact per swing, effect cleanup, squash, floor support, settling and rematch restoration. Close-ups are included as combat-*.png in the prepared project.

Final polish regression: 250/250 PlayMode tests passed on 2026-11-10, including scene reset and guided-tour replay.

## Outcome performances — 2026-11-10

After the defeated skeleton finishes falling and the result theme starts, the winner performs a 4.6-second celebration: small squash-and-stretch hops, breadstick air guitar and a bow. Either skeleton can celebrate, according to the result. The winner turns visually toward the camera; the controller stays in place. Hand IK keeps the breadstick attached, the defeated character stays down, and attacks cannot resume during the result.

Mr Broccoli acknowledges a player victory with an approving nod, raised arm and bouncing crown. On player defeat he shakes his head and covers his face. Two timed captions accompany each outcome. These are text reactions, not new recorded voice-over. The outcome clock pauses with the game, resets on rematch and does not start another music cue.

The result panel sits in the lower-right corner so it does not cover the celebration. A small live portrait of the actual broccoli model sits in the lower-left corner, showing the distant manager's reaction alongside his caption. Its disabled camera renders only during results into one reusable 480x256 target; camera and target are disposed when the scene resets. No new art/audio assets or scene-authoring pass are required.

Outcome verification: 253/253 PlayMode tests passed on 2026-11-10 with editor audio output muted. New coverage checks delayed celebration, winner selection, distinct captions, one result cue, pause-safe poses, rematch cleanup and portrait disposal on reset.

## Breadstick combo and block rebound — 2026-11-10

Three successive confirmed contacts form BONK → BACK-BONK → SOUP SMASH: a sideways cut, a mirrored backhand and a two-handed overhead chop. The torso twists with each cut; the final windup squashes and leans back before pitching forward. Both hands return smoothly to the ready pose. The third strike has a stronger horizontal push, while every strike still deals 25 damage through the swept visible weapon. No contact means no combo advance. The next chain starts at BONK after the finisher.

Click LMB again within 1.05 seconds of the last contact to continue. A single next attack can be buffered in the final 0.22 seconds of recovery; repeated clicks do not queue a whole sequence. Follow-ups include a short collision-aware step to maintain reach after knockback. A miss, timeout, accepted guard, incoming damage or encounter reset clears the chain and buffered input. The small gold combat label shows the current strike and whether the next one is queued.

A successful frontal block visibly throws the enemy's stick back, rocks its skull and ribs, compresses the player's guard and expands the cyan accent. The CLACK prompt invites releasing RMB and countering with LMB; the rival complains that its breadstick betrayed it. The block itself deals no damage. Rebound and combo clocks stop on pause, and all transient state clears on rematch or exit.

Four additional PlayMode cases exercise three actual contacts without repositioning between strikes, single-slot buffering and guard cancellation, miss/timeout/rematch cleanup, and the no-damage block rebound followed by a counterattack. Post-pose close-ups are included as combo-*.png in the prepared project. Existing attack, hit and block SoundEvents remain distinct; no additional placeholder sounds are introduced.

Combo regression: 257/257 PlayMode tests passed on 2026-11-10 with editor audio output muted. The outcome test now samples motion throughout its observation interval, avoiding phase aliasing between two snapshots while checking the defeated actor stays still.

## Rival repertoire — 2026-11-10

The rival cycles through three readable patterns. DOUBLE DIP is a right cut followed by a backhand: 0.70-second first windup, 0.35-second swing, a 0.24-second pause, then a separate 0.58-second backhand windup and a short step in the committed direction. SOUP HAMMER raises the breadstick with both hands, squashes the torso and rocks the head for 0.95 seconds before chopping down. BAGUETTE LUNGE draws the stick back at waist height for 0.80 seconds, then pokes forward with a small collision-aware step. Swings remain 0.35 seconds and every physical contact deals 20 damage once.

The direction locks when a pattern starts and stays fixed through both DOUBLE DIP hits. The rival cannot steer an active swing toward a dodging player. Move sideways during each windup, or face the rival and hold RMB. Keep moving for the second helping: its short forward step can catch a player who stops near the edge of the first swing. The backhand fan extends farther to show that extra reach. A successful frontal block cancels the remaining combination and triggers the existing recoil/counter window. A player hit also interrupts a pending attack. After a completed combination or thrust the rival rests for 0.85 seconds; the overhead attack leaves 1.05 seconds of recovery. Rematch restarts with DOUBLE DIP.

The windup label beneath the enemy HP bar names the pattern and shows preparation progress. A small amber fan marks the double's direction; coral and lavender lanes distinguish the overhead strike and thrust. These are anticipation cues: damage still requires the animated weapon to touch the player, with an unobstructed path. Cues hide when interrupted, released, reset or leaving battle, and freeze on pause. Each move has a short kitchen-themed caption.

Five additional PlayMode scenarios cover all four swing poses and consecutive real contacts without repositioning the player between attacks, damage-free blocks of every pattern with cancellation of the follow-up, walking sideways out of the committed attacks, pause/interruption/rematch cleanup, and obstruction by a physical wall. Close-ups are saved as enemy-windup-*.png. Existing attack, hit and block events are reused; audio content remains placeholder material.

The base face pose now runs before combat reactions and floor support. Defeated heads stop their idle animation; support bounds include the initially inactive breadstick, so the complete final pose stays above the floor.

Rival repertoire regression: 262/262 PlayMode tests passed on 2026-11-10 with editor audio output muted. All new attack patterns, ordinary walking dodges, blocked combinations, natural consecutive hits and final-pose floor support passed.
