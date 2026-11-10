# MCP acceptance — 2026-11-10

Environment: macOS, Unity 6000.3.21f1, isolated AudioMW-BadShift project, local Node stdio transport. All editor/runtime QA output muted. No human listening acceptance or production audio was involved.

## Observed client workflow

- Claude Code `mcp get audiomw-bad-shift` reported Connected using the real companion process. Its non-interactive chat acceptance attempt failed before tool use: OAuth session expired and could not be refreshed. Claude authentication was not changed. A full Claude chat-to-plan acceptance remains pending re-authentication.
- Codex CLI actually called `audiomw_inspect` and `audiomw_propose` with two local WAV fixture attachments. Unity returned plan b405b551536b44ab9a2d96e87dcc6fea, pending. Before approval, the existing MicrowaveRandom asset was byte-identical and the planned import folder did not exist.
- The native Unity Agent Review showed the exact event path, old clips, preserved Random selection, gain −10.5 dB, SFX route and both attachment hashes/destinations. The final confirmation dialog was observed, followed by the applied state. An attempted Cancel observation overlapped an application state change; it is not counted as a verified native Cancel test.
- Codex separately called inspect and plan_status and received applied, two imported clips and one updated event. Both copied WAV files matched the reviewed SHA256 values.
- A focused PlayMode acceptance test loaded RelayField, selected Random mode, placed the player at the microwave and called the existing gameplay Interact path. The runtime debugger recorded Played with one of the two imported clips, the original SFX route and gain, and the microwave response animation triggered. This test passed 1/1 with editor output muted.
- After acceptance, the exact original MicrowaveRandom asset was restored and only the identified test import folder was removed. The delivered demo keeps its original placeholder audio. Test request/reply and acceptance evidence remain outside Assets.

## Automated coverage

- 297/297 EditMode tests passed, including 12 new agent cases: no-write proposal/reject, preserved event identity/settings, real clip decode/import, repeat apply, changed attachment/target, wrong session/project, destination conflict, invalid file/path, explicit routing for new events, unsupported container rejection, immutable review data, no remote approval, restart invalidation and mailbox retry/conflicting-ID behavior.
- 6/6 Node tests passed, including actual official-SDK stdio negotiation/tool discovery, inspect binding, restart/heartbeat/path checks, attachment resolution, request identity, cross-client ID conflicts and timeout reporting.
- 272/272 full PlayMode tests passed with the original demo clips restored. The 15 distribution/tool tests passed separately.
- The 1/1 imported-clip gameplay acceptance above is separate from the unchanged demo suite. One repeat Editor run received a native Save Scene shortcut while a test window was open and failed on the batch-mode dialog assertion; a headless-graphics retry was unsuitable for the existing UI Toolkit view tests, so normal graphics were restored for the final run. Full PlayMode and final package checks are recorded in the prepared project's AGENT-MCP-VERIFICATION.json.
- Native review was inspected at narrow and maximized sizes. The long proposal-list label was subsequently shortened to status/event count/ID, with the full purpose retained in the review and tooltip.

Evidence in the development workspace: work/bad-shift/agent-final-edit.xml, agent-imported-play.xml, agent-codex-events.jsonl, agent-codex-status.jsonl, agent-applied.json and agent-claude-result.json. Final artifacts include a compact verification report; local logs may include machine-specific paths.

## Limits

This verifies local files exposed by the chat client, not cloud binary upload/attachment APIs. New events still need gameplay bindings. Music stems, VoiceLine assets, runtime loudness/advice, Unreal and other operating systems were not added or certified. The bridge's approval flow does not sandbox unrelated shell or Unity-evaluation tools running as the same user. Read agent-mcp.md for the exact scope and local trust boundary.

## Adaptive music follow-up — preview.18

The SDK stdio client called `audiomw_propose_music` in the actual BAD SHIFT editor with five test WAV imports, replacements for Explore/Combat and the existing Music_States profile. Plan `868b4dc7ab684821ae21fe22b0bf1af3` was pending with all three original assets byte-identical and no import folder.

The native review showed the previous contents and preserved −14.9 dB gain/MusicDuck route, 120 BPM, 4 beats/bar, per-file mono/source-layout choices, Intensity weights and Bar/0.3s/EqualPower transitions. The native confirmation dialog and subsequent applied state were observed. As with the prior pass, a Cancel automation attempt overlapped a changed native application state; native Cancel is not independently certified.

All five imported WAV bytes matched the plan hashes. A separate PlayMode gameplay test passed 1/1: the existing BAD SHIFT RequestMusic hook selected Explore/Combat, Intensity changed layer weights, quantized transitions completed both ways, and the imported bass source was playing internally while editor audio output stayed muted. It verified a stereo bed, mono pulse, unchanged 0.18 track gain and shared original route. After acceptance, the exact original Explore/Combat/Music_States assets were restored and only the identified temporary imports/test were removed.

The targeted agent suite passed 22/22 before the final constant-weight/intro case was added. The final complete EditMode suite passed 308/308, including 11 new music cases. Mono testing verified stereo → mono → stereo using the native AudioImporter, channel content, unchanged source bytes/GUID/sample count and retained track references. The 7/7 Node protocol/transport tests and 15/15 distribution tests also passed.

The first targeted run exposed Unity JsonUtility's materialization of an empty optional nested profile. Added an explicit review presence flag; the repeated suite passed, including existing SoundEvent plans without music. No runtime music-player workaround was introduced.

The final full PlayMode suite passed 272/272 with original music restored, and the macOS development build succeeded with zero errors. Final package parity and integrity results are in the prepared project's MUSIC-MCP-VERIFICATION.json. Evidence: work/bad-shift/music-agent-pending.json, music-agent-applied.json, music-agent-play.xml, music-agent-edit2.xml and music-release-*.xml/log. Full Claude chat acceptance is still subject to its prior authentication limitation; this pass used the shared official-SDK MCP transport.
