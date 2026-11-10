# BAD SHIFT final gameplay handoff — 2026-11-10

This pass freezes the requested gameplay/visual scope so that sound design can proceed. The demo remains an AudioMW showcase with placeholder audio, not a completed Asset Store release.

## Final behavior

- At 50 HP the Breadstick Warden enters overtime; a lethal physical stick contact triggers a corkscrew clay fall, dizzy accents and a delayed gold/mint victory with confetti. Victory music begins only after the fall; normal background music is silenced. Defeat keeps its distinct result theme.
- The complete bathroom is centered on the green lane at (0, 0, 2), with sink, toilet and functioning door. The picnic table is farther forward. The pendant and floor EXIT marker are hidden.
- CAFETERIA is centered using rendered text bounds with at least 0.2 world units of inset on each side of the cream band.
- Four tall, invisible static collision boundaries contain the playable area, including jumps and knockback. Decorative low walls remain visible.
- Repeated landing impulses no longer accumulate unbounded compression. The body spring stays inside a finite deformation range; leg IK limits reach and releases an unreachable planted contact.
- Normal launch and scene Reset restore Sound on. Development batch runs mute the editor output; the standalone -audiomw-silent flag retains silence through Reset. The flag is not a shipping default.
- Audio map / Tab toggles live source-to-player links and the event journal. It explains distance attenuation, VO ducking, room processing and selected microwave clips. The distance curve is labelled as a model; captured event gain excludes mixer/distance/master processing and is not LUFS. Combat/invitation hides the explanatory overlay.

## Sound-design work

Open Assets/Playground/RelayField.unity. Replace placeholder clips through the existing SoundEvent, MusicTrack and VoiceLine assets, preserving their asset GUIDs and gameplay references. Review levels on the actual mixer routes before changing master gain.

Battle files are in Assets/BadShift/Battle: Attack.wav, Hit.wav, Block.wav, Challenge.wav, Victory.wav and Defeat.wav. The Challenge event currently also marks overtime. Attack/Hit/Block are separate physical events. The final bonk currently uses Hit; author a distinct finishing cue only if it improves the sound design.

General placeholders are in Assets/Playground/Audio. Music stems require matching musical duration, tempo and loop boundaries. Microwave random/bag variation, surface footsteps, ceramics, furniture, fridge loop, doors and bathroom taps have separate authored events/components. The sink/toilet currently reuse the room source; replace this deliberately rather than assuming they already have bespoke recordings. Voice lines remain synthetic cues with subtitles.

Use normal launch to listen. For visual development, launch through LaunchServices with -audiomw-silent and verify Sound off before interacting; do not attach UI automation early enough to spawn a second unflagged instance.

## Remaining product work

The Unity editor Design 02 and agent/mix audit is in Documentation/agent-and-mix-audit-2026-11-10.md in the source repository. The preview.17/18 passes add the local MCP bridge, confirmed SoundEvent imports and adaptive music/stem plans; see Documentation/agent-mcp.md and Documentation/agent-music-mcp.md. Live rendered-master LUFS and the Unreal adapter remain unimplemented. Production audio, human listening and shipping platform qualification remain open.

## Verification for this handoff

The final Unity 6000.3.21f1 PlayMode run passed 272/272 tests, including 85 demo tests. The focused corrected regression passed 13/13. The macOS Mono Development build completed with zero errors. Unity render captures cover layout, repeated furniture impacts, the finishing fall and victory. Native observations and package parity are recorded separately in BAD-SHIFT-VERIFICATION.json. Native QA then caught an IMGUI arrow transform error at a scaled window; its drawing transform was corrected and the player rebuilt. Gameplay tests precede this drawing-only correction. These checks do not replace human listening or a full human playthrough.
