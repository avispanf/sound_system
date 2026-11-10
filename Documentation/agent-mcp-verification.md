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
