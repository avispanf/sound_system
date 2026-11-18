# BAD SHIFT — AudioMW demo

A separate visual/gameplay variant of Relay Field, originally based on AudioMW preview.16
(commit c9cb351), now using preview.21 with the optional local MCP companion. Runtime and original Relay Field project are preserved. The
AudioMWPlayground.cs here is a demo-only integration snapshot: it hides the old HUD
when BadShiftDemo exists and adds an audition music multiplier.

## Play

Open Assets/Playground/RelayField.unity in the prepared AudioMW-BadShift project
using Unity 6000.3.21f1. Or launch Build/RelayField.app (window title BAD SHIFT).
WASD / Shift / Space move. E uses the nearest object: microwave, boss, bathroom, lower fridge, loose props or the sparring skeleton. G operates the upper fridge door. Walk through three bananas; E collects the other four foods; collect all seven to complete the ingredient route. R resets. Sound on/off mutes without pausing. Normal launch and Reset start with sound enabled; automated QA uses a separate silent flag.

- Microwave: approach the coral appliance, E (or X) plays an actual AudioMW container;
  Q switches Random / ShuffleBag. HUD shows the actual debugger clip name.
- Kitchen: cross the coral line. Pending bar-boundary timing comes from MusicPlayer.
  The employee's pose/light changes with the actual current track; VO freezes staff.
- Broccoli: approach the lectern, E (or X) queues VO; V also queues globally. Bus duck gain
  comes from the installed AudioMixer. The current clips are tones with subtitles.
- Fridge: walk the right-hand lane. A fixed looping SoundEvent uses spatial
  attenuation. HUD measures the real player AudioListener distance. The optional audio map labels its distance-gain model separately from measured mixer values.
- Bathroom: X plays the same impact, Q switches Dry / Echo / Reverb. Real Unity
  AudioEchoFilter and AudioReverbFilter act on one routed source; music is reduced
  while standing in this station. Echo delay 280 ms; reverb Bathroom preset.
- Floors: original local Surface Switch / ShuffleBag footsteps remain active.

P pauses. M requests music; L exercises the voice limit.
All audio remains synthesized placeholder content. No human listening sign-off.

## Reproduce in an isolated copy

Copy the prepared Relay Field project (Assets, Packages, ProjectSettings), retaining
its installed mix profile, original scene and .audiomw-playground marker. Use an empty
destination; add .bad-shift-demo. Add com.unity.modules.animation version 1.0.0 to
Packages/manifest.json. Copy Runtime files to Assets/PlaygroundRuntime, Editor files
to Assets/Editor/PlaygroundSetup and Tests to Assets/PlaygroundTests. The existing
Playground assembly definitions and BuildAudioPlayground helper are required.

The prepared project includes the required art under Assets/ThirdParty. Sources:
KayKit Skeletons 1.0 and Restaurant Bits 1.0 (official KayKit-Game-Assets GitHub
repositories), Kenney Food Kit 2.0. Preserve LICENSE.txt/License.txt and SOURCES.md.
Skeletons includes Idle, Walking_A and Cheer; no separate animation pack is needed.

In the isolated project run Unity -batchmode -projectPath <project> -executeMethod
BuildBadShift.Run, then PolishBadShift.Run, then PolishBadShift.FinishAndBuild.
Use separate log paths. BuildBadShift refuses an existing Assets/BadShift folder.
Run these only for a fresh fixture, not after hand editing. Normal iteration uses
the already-authored scene. The original mix receipt deliberately detects the
changed demo scene; don't reapply its old installation plan.

## Verification and boundaries

272 PlayMode tests pass in Unity 6000.3.21f1, including eighty-five demo tests: five inherited gameplay/mix tests and
eighty BAD SHIFT checks (real clip selection, source/filter switching, listener
measurement, actual music/VO animation timing, collision, live character reactions and walkable routes). Collection tests move the character
programmatically; they are not proof of complete human navigation/listening QA.

Native visual review catches model placement and HUD issues. Echo/reverb components
and playback are tested; the perceptual difference and tails still need listening.
No live acoustic measurements or audio-quality approval is claimed. The visual
scene is an implementation inspired by the concept, not a reproduction of its art.
Animation is deliberately simple, and dining furniture is dynamic while appliance housings retain static collision while their doors move. See GUIDE-AUDIT.md for the complete current checklist.

## Physics and animation update

After base authoring, run ImproveBadShift.Run, then BuildAudioPlayground.BuildPlayer.
This applies physical furniture, adjusted circulation, NPC colliders, player/staff
animation controllers, floor joints and the exit frame. Movement now accelerates,
brakes and uses actual controller velocity for footsteps. The manager nods/blinks,
staff idle between demonstrations, and VO freezes staff/player motion.

## Reference diorama and clay animation

The current prepared scene includes the reference art pass. See REFERENCE-ART.md
for visual changes, animation behavior and one-time authoring order.

## Interactive physics and orbit camera

See INTERACTION-PASS.md for camera controls, automatic banana collection,
moving furniture, breakable cups, boss speech bubbles and complete scene reset.

## Presentation and guided demo

See PRESENTATION-PASS.md for the dark UI, resizing and guided tour.
In the prepared Unity project use Tools > AudioMW > BAD SHIFT Demo, then
Watch demo in Game View or Play yourself. The same two modes and Reset are
available in the player. Use Free Aspect in Game View when resizing its panel.

## Aligned cafe and refrigerator doors

See LAYOUT-PASS.md for the three dining rows, kitchen alignment, furnished bathroom, faster door and independently opening refrigerator compartments.

The latest character pass replaces visible skeleton meshes with rounded clay geometry, bone-attached aprons, blinking eyes and stepped head animation. See REFERENCE-ART.md for authoring and lighting details.

The gait pass adds longer legs, a smaller head and narrower apron, world-anchored stance feet, distance-driven swing and contact-driven footsteps. ClayGaitPass.Run applies it once after the character pass.

ClayPerformancePass.Run adds delayed arm/hand motion and music-clock dance accents after the gait pass. Running, jump release, landing and VO/pause holds have dedicated PlayMode coverage.

## Cafe life and contextual interaction

See CAFE-LIFE.md for E interactions, doors at touching distance, broccoli reactions,
breadstick sparring, health, death/respawn and the sound toggle. CafeLifePass.Run
authors these once after ClayPerformancePass.Run in an isolated demo copy.

## Breadstick Warden battle

See BATTLE-ARENA.md for the relocated opponent, proximity invitation, telegraphed
combat, health bars, red/blue lighting and smoke, separate attack/hit/result audio,
and persistent victory/defeat with rematch/reset. This supersedes the automatic
three-second sparring reset in the first cafe-life pass.

Battle controls: E challenges; LMB attacks; hold RMB to block; MMB orbits the camera.
Defeated characters fall before the result panel and its separate music theme.

The clay combat polish adds body-led anticipation, elastic hit reactions, two-handed guarding, visual swing recovery and a supported squash-and-flop death. The existing controls and damage rules are unchanged. See BATTLE-ARENA.md for the sequence and validation details.

## Quiet development and QA

Automated batch editor runs in the marked BAD SHIFT project mute the editor audio output through BadShiftQuietBatch. Playback simulation, event records, music scheduling and game time remain active. Do not run audible development/visual QA sessions unless the user asks for listening.

For standalone visual QA, launch the executable with -audiomw-silent. It starts with Sound off before scene playback and keeps that initial mute through scene resets. Sound on can still be selected explicitly. A normal launch without this argument retains the regular sound default.

The result performance adds a winner-only hop, breadstick air guitar and bow, plus separate broccoli approval/facepalm gestures and timed captions. The live manager portrait and result controls leave the winning actor visible.


The breadstick combo adds three contact-driven LMB strikes: BONK, BACK-BONK and the two-handed SOUP SMASH. Time another click after a hit to continue; one late click can be buffered. Holding RMB breaks the chain and raises guard. A successful block visibly rebounds and briefly stuns the rival, with a cyan CLACK prompt for a counterattack. See BATTLE-ARENA.md for timings and checks.


The rival now alternates DOUBLE DIP (two separately telegraphed sideways swings), SOUP HAMMER (a slow overhead chop) and BAGUETTE LUNGE (a committed straight thrust). Read the pose and floor direction cue, walk sideways or hold RMB. A successful block stops the remaining combo; the longer recovery after heavy attacks gives time to counter.


At 50 HP the rival enters OVERTIME with a short elastic tantrum, amber/violet lighting and two new combinations: TRIPLE SERVING and RUSH ORDER. Each strike keeps its own readable windup and direction cue. Frontal blocks cancel the remaining series; the transition remains vulnerable. See BATTLE-ARENA.md for timings, reset behavior and verification.


See FINAL-HANDOFF.md for the final gameplay freeze, sound replacement map, central bathroom, collision enclosure, bounded clay deformation, overtime finishing sequence and live audio explanation overlay. The Audio button or Tab cycles labels → details → off. Labels are the default; details show the journal and nearby links. Invitations hide the overlay; combat retains short event labels.

## Agent integration

Open Window > AudioMW > Agent Review and enable the local bridge for this editor session. The prepared project includes AgentBridge (run npm ci --ignore-scripts there) and Documentation/agent-mcp.md. Claude Code/Codex can propose local audio imports and existing SoundEvent clip replacements; changes require confirmation in Unity. The fixture helper BadShiftAgentReview.Open opens this window with editor audio muted. See AGENT-MCP-VERIFICATION.json for this separate editor integration pass. Gameplay and original placeholder clips are preserved.

Preview.18 adds `audiomw_propose_music`: reviewed stems, Intensity bindings and Explore/Combat transition rules. See Documentation/agent-music-mcp.md and MUSIC-MCP-VERIFICATION.json. Original demo music is restored after silent acceptance.

Preview.19 adds nearby, transient sound-event labels and distance-limited links. See AUDIO-VISUALS.md for controls, filters and verification.

Preview.20 develops the native Events/Inspector workflow from the v1.1 editor guide: explicit dry/runtime audition, actual routing and parameter references, Review changes access and Mute with Undo. See Documentation/editor-ui-stage-one.md and EDITOR-UI-VERIFICATION.json. Screenshots are actual Unity captures.

Preview.21 adds Spatial, Parameters and Routing workspaces. Native light/dark checks and 314 EditMode tests are recorded in SPATIAL-UI-VERIFICATION.json; see Documentation/editor-context-workspaces.md. Runtime and gameplay build remain unchanged.

Preview.23 completes the Dark Music/Diagnostics review with real muted runtime requests, narrow two-line diagnostic rows and consistent workspace spacing. See DARK-UI-VERIFICATION.json: 315 EditMode, 32 targeted PlayMode and 16 runtime UI assertions passed. Light was excluded by the latest user decision. The native gameplay build was not rebuilt for these Editor-only changes.

Preview.24 polishes the native SoundEvent Inspector and Review changes in Dark. See INSPECTOR-REVIEW-VERIFICATION.json and Documentation/editor-context-workspaces.md. Proposal review does not apply or import anything; whole-plan application still requires human confirmation.

Preview.25 refreshes shared dark workspace styling, the Events library, searchable Music profiles and music/profile review cards. Evidence and precise validation scope are recorded in DARK-UI-VERIFICATION.json; this editor-only update does not rebuild the native player.

Preview.26 applies the reference library/overview/context composition to Spatial, Parameters and Routing, including narrow reflow and persistent selection. See DARK-UI-VERIFICATION.json for fresh checks and limitations. Runtime and native player remain unchanged.

Preview.27 gives Diagnostics a responsive captured-detail column and Events an actual source duration/channel table. DARK-UI-VERIFICATION.json distinguishes fresh Editor checks from unperformed runtime/listening checks.

Preview.28 restores selected diagnostic details after rebuilding and exposes actual captured limit/cooldown/voice context. Its muted normal-Editor runtime acceptance is recorded separately from EditMode and listening results in DARK-UI-VERIFICATION.json.

Preview.29 adds a real project-event library and identity-based request filtering to Diagnostics. Requested-event scope stays visible in narrow windows; native selection and captured detail are preserved. See DARK-UI-VERIFICATION.json for fresh runtime and Editor evidence.

Preview.30 separates Events source overview and asset context, with narrow reflow preserving source selection and audition controls. Native Inspector authoring and pin/follow remain unchanged. See DARK-UI-VERIFICATION.json for validation scope.

Preview.31 improves narrow native Inspector object references and restored foldout summaries. Review changes remains a separate human-confirmation workflow; fresh test and native audit scope are recorded in DARK-UI-VERIFICATION.json.

Preview.32 keeps long weighted-source names readable in narrow native Inspectors. Weighted and Switch child layout audit used a transient QA copy; no new demo content or runtime behavior is included.

### Editor UI preview.33

Music profile native Inspector now groups states, default timing, route overrides and runtime preview, with stacked controls at narrow widths. Fresh 327/327 EditMode and 5/5 package checks; silent native 300 px empty QA profile inspection. No new native gameplay build or listening check. See DARK-UI-VERIFICATION.json for exact scope.

### Editor UI preview.34

Music transition rules show source → destination and consistent timing labels. Narrow native Inspector alignment now handles nested controls; wide alignment is restored on resize. Fresh 328/328 EditMode and 5/5 package checks, muted native Inspector captures at 300/520 px. QA profile only; no new gameplay build or listening check.

### Editor UI preview.35

Native mixer routing profiles now have grouped fields, honest exposed-name checks and a native mixer action. Multi-edit remains supported. Fresh 329/329 EditMode and 5/5 package checks; muted QA Inspector captures at 300/520 px. Runtime unchanged; no new gameplay build or listening test.

### Editor UI preview.36

Review shows routing and channel handling before expanding details, with captured platform/Unity context. Fresh 329/329 EditMode and 5/5 package checks; native QA pending-plan screenshots at 1200/360 px. No plan applied, no new gameplay build or listening check.

### Editor UI preview.37

Mixer binding headings show parameter → exposed target and explicit mixed/unassigned states. Fresh final 330/330 EditMode and 5/5 package checks, muted native expanded/collapsed 300 px QA captures. Runtime unchanged; no new gameplay build or listening test.

### Editor UI preview.38

Music uses compact state buttons and a separate profile-rule context. Fresh 330/330 EditMode, 5/5 package checks and 30/30 silent normal-Editor runtime assertions on BAD SHIFT. Main-editor screenshots at 1200/360 show QA edit-mode composition only. No new native player build or listening acceptance.

### Editor UI preview.39

Sound Parameter Inspector now separates stored/effective defaults, range and scope, preserving native multi-edit and Undo. Fresh332/332 EditMode and5/5 package checks; native300px inspection and live reference-navigation check without sound. No new gameplay build or listening check.

### Editor UI preview.40

MusicTrack Inspector groups sources, grid, layers and output with native gain and multi-edit. Fresh334/334 EditMode and5/5 package checks; actual track inspected at300px without sound or asset edits. No new gameplay build or listening check.

### Editor UI preview.41

AttenuationPreset Inspector separates stored/applied distances and preserves inactive custom curves, with direct Spatial navigation. Fresh335/335 EditMode and5/5 package checks; native300px inspection and navigation without sound or asset edits. No new gameplay build or listening check.


### Editor UI preview.42

Music layers show authored names and controlling parameters in collapsed rows; expanded fields remain native at300px. Fresh336/336 EditMode and5/5 package checks, plus native actual-track visual inspection without sound. No gameplay rebuild or listening check.


### Editor UI preview.43

Long music layer headings wrap inside300px native Inspectors. Fresh336/336 EditMode,5/5 package checks and30/30 real Editor runtime assertions for Music/Diagnostics; output muted. Native long-name QA screenshot included. No gameplay rebuild or listening check.


### Editor UI preview.44

Repeated Pin reuses native profile Inspectors by asset identity and leaves selection independent. Fresh337/337 EditMode and5/5 package checks; actual PARAM_Intensity repeated pin visually verified at300px without new windows or sound. No fresh runtime probe/gameplay rebuild/listening; preview43 runtime report remains separate.

### Café lighting and street traffic

The lighting button in the top bar cycles through Auto, Night and Day. Auto starts at sunset and runs a 96-second cycle. Night brings up two neutral-white ceiling fixtures. Sunrise and sunset warm the directional light and reveal three shafts of light inside the café windows. The combat presentation blends over the current time of day.

One car uses the street at a time. Four finishes, alternating directions, different speeds, roof accessories and suspension movement distinguish successive passes. The motor follows the car as an AudioMW event. The transformer's hum stays at the sign beside the dining windows. Both loops are replaceable mono demo recordings synthesized by the supplied script. Motor Doppler is 2.1 and neon Doppler is zero.

To reproduce the authored pass in a BAD SHIFT project, copy the matching runtime and editor sources, generate the two WAVs with `python3 Tools/BadShift/Audio/generate_street_audio.py <project>/Assets/BadShift/Night`, and place `Tools/BadShift/Art/CafeSunShaft.shader` in that folder. Run `BadShiftNightPass.Run` using the isolated batch authoring project. Save the authored scene, new assets and their Unity metadata together. The pass recognizes an already-authored street.
