# BAD SHIFT — AudioMW demo

A separate visual/gameplay variant of Relay Field, based on AudioMW preview.16
(commit c9cb351). Runtime and original Relay Field project are preserved. The
AudioMWPlayground.cs here is a demo-only integration snapshot: it hides the old HUD
when BadShiftDemo exists and adds an audition music multiplier.

## Play

Open Assets/Playground/RelayField.unity in the prepared AudioMW-BadShift project
using Unity 6000.3.21f1. Or launch Build/RelayField.app (window title BAD SHIFT).
WASD / Shift / Space move; E collects five foods; reach the blue exit. R resets.

- Microwave: approach the coral appliance, X plays an actual AudioMW container;
  Q switches Random / ShuffleBag. HUD shows the actual debugger clip name.
- Kitchen: cross the coral line. Pending bar-boundary timing comes from MusicPlayer.
  The employee's pose/light changes with the actual current track; VO freezes staff.
- Broccoli: approach the lectern, X queues VO; V also queues globally. Bus duck gain
  comes from the installed AudioMixer. The current clips are tones with subtitles.
- Fridge: walk the right-hand lane. A fixed looping SoundEvent uses spatial
  attenuation. HUD measures the real player AudioListener distance, never guessed dB.
- Stockroom: X plays the same impact, Q switches Dry / Echo / Reverb. Real Unity
  AudioEchoFilter and AudioReverbFilter act on one routed source; music is reduced
  while standing in this station. Echo delay 280 ms; reverb Bathroom preset.
- Floors: original local Surface Switch / ShuffleBag footsteps remain active.

Tab opens diagnostics. P pauses. M requests music; L exercises the voice limit.
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

Nine PlayMode tests pass in Unity 6000.3.21f1: five inherited gameplay/mix tests and
four BAD SHIFT checks (real clip selection, source/filter switching, listener
measurement, actual music/VO animation timing). Collection tests move the character
programmatically; they are not proof of complete human navigation/listening QA.

Native visual review catches model placement and HUD issues. Echo/reverb components
and playback are tested; the perceptual difference and tails still need listening.
No live acoustic measurements or audio-quality approval is claimed. The visual
scene is an implementation inspired by the concept, not a reproduction of its art.
Animation is deliberately simple, and furniture is mostly decorative.
