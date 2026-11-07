# AudioMW Relay Field — Unity 6 manual integration playground

Open this project with Unity **6000.3.21f1**. Open **Assets/Playground/RelayField.unity**
and press Play. Click the Game view for keyboard focus. The project starts from
Unity's installed **3D URP 17.0.14** template, with an original small collection game
added for AudioMW testing. It is not Unity Starter Assets or a bundled Microgame.

AudioMW is installed from **Packages/AudioMW.tgz**; the project is portable and does
not depend on the development Sound System folder. No source project scene was
replaced. URP and Input System packages are resolved normally by Unity. The template dependencies are upgraded to URP 17.3.0
and Input System 1.20.0 for this Unity 6.3 editor.

## Play

Collect five gold relays with E, then reach the blue exit in the red zone.
WASD moves, Shift runs, Space jumps. Camera follows without mouse capture.

| Action | What to test |
| --- | --- |
| Walk/run across gray / green / blue floor | Local Switch footstep states Stone / Grass / Metal; shuffle bag and variation |
| Space, then land | Landing one-shot and spatial placement at the player |
| F | Impact one-shot three meters ahead, marked by a brief sphere |
| E beside a gold relay | Pickup and beat-quantized stinger; mission progress |
| Enter/leave the red zone | Music profile transition on bar boundary; layered intensity and ambience crossfade |
| M | Request the other music state; repeat/replace requests and pending transition display |
| V repeatedly | Queue synthetic VO cues, subtitles, duck and release |
| L | Three loop requests with a two-per-emitter limit; captured rejection appears in HUD and Diagnostics; loops stop after 1.2 seconds |
| 1 | Mute/unmute music to isolate other sounds |
| P | Pause/resume gameplay and audio |
| R | Restart the scene, releasing the previous scene's bank lease and playback |
| H | Toggle help |
| F8 | Save a runtime snapshot; Editor: TestReports folder in this project; full path in Console/HUD |

The AudioListener is at the player's ears, not on the distant overview camera.
Music ducking uses the game's adapter to MusicPlayer.SetVolume; ambience uses real
AudioMW parameter bindings. This is explicit integration code, not a new hidden
middleware feature. Scenes and all audio assets remain editable in Inspector.

## Audio content

**All included audio is existing synthesized placeholder content.** VO cues are
musical tones with subtitles, not spoken dialogue. Surface sounds are deliberately
pitch-distinguished blips, not convincing footsteps. Do not sign off sound design
quality from them. Replace WAVs in **Assets/Playground/Audio**, or reassign clips in
**Assets/Playground/Data**, with your own redistributable recordings/stems.

After replacement, check loop length/BPM alignment, crossfades, loudness, variation,
stereo placement and duck release by ear. In a test note, record what you heard,
not just the HUD counters. Rebuilding with BuildAudioPlayground.Run recreates the
generated Data assets: normal editing/testing does not require rebuilding.

## Suggested 15-minute pass

1. Walk on all three surfaces; run and stop. Listen for missing steps or repeats.
2. Jump repeatedly and fire while moving. Check voice placement and overlaps.
3. Enter/leave the red zone before and after a pending transition boundary.
4. Queue V twice. Verify subtitles, audible duck and full recovery afterward.
5. Trigger L, then open Window > AudioMW > Diagnostics and inspect the real limit.
6. Pause during music/VO, resume, finish the relay route, then restart with R.
7. Save an F8 snapshot and add subjective findings: action, expected, heard,
   repeatable yes/no, clip names, and whether this is placeholder or real content.

This project is a manual test fixture, not an Asset Store release or listening
approval. Windows/mobile/IL2CPP and real Addressables are separate gates.
