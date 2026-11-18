# Timeline editing through MCP

Studio 54 adds `audiomw_propose_timeline` to the existing local companion for Claude Code, Codex and other stdio MCP clients. Update both the Unity package and the companion directory, then reconnect the client so its tool list refreshes. The companion includes scene binding and timeline proposals. Approval takes place in Unity. Keep the Editor in Edit Mode and enable the local session in **Window > AudioMW > Panels > Authoring > Agent Review**.

## Workflow

1. Call `audiomw_inspect` to confirm the intended project/session and list arrangements and imported source GUIDs.
2. Call `audiomw_inspect` with `arrangementGuid` to retrieve that arrangement's `Timeline.Detail`: JSON containing its current `Hash`, lane/clip/cue IDs and authored timing. The initial inventory deliberately does not embed every timeline.
3. Call `audiomw_propose_timeline` with that GUID, `expectedHash`, a unique 32-character lowercase hexadecimal `requestId`, intent and ordered edits.
4. Call `audiomw_show_review`. Unity shows the saved and proposed arrangements plus exact operations. The developer confirms the complete plan through the Unity button and dialog, or rejects it. Agents must not invoke approval through scripting, reflection, shell or UI automation.
5. Poll `audiomw_plan_status`. `pending` is not applied. Compilation/disconnect expires plans; inspect and propose again. Retry an uncertain timeout only with the same request ID and identical input.

No source files, assets or scenes change while proposing. The review captures target identity, serialized content and fingerprints of the arrangement and dependencies, including importer metadata. Unsaved or changed targets/dependencies invalidate the plan. Application preserves the arrangement GUID, surviving lane/clip identities, routing and complete parameter curves. It creates one Undo step and saves only the target asset. Save after Undo if the reversal must persist across reopening.

## Supported edits

- `edit_clip`: `clipId`, `start`, `trim`, `duration`, `fadeIn`, `fadeOut`, `gain`, `repeat`. Supply every field; source and ID stay unchanged.
- `add_clip`: `laneId`, imported `sourceGuid`, and the same timing/gain/repeat fields. No file copy or import.
- `duplicate_clip`: `clipId`, destination `laneId`, new `start`. Other clip fields are copied and a new ID is assigned.
- `remove_clip`: `clipId`.
- `add_lane`: `name`, `gain`. Creates an empty track inheriting the arrangement output. Its generated ID is available after human application and re-inspection; adding clips to it requires a subsequent plan.
- `edit_lane`: `laneId`, `name`, `gain`. Retains its binding and routing.
- `remove_lane`: `laneId`, including its clips, explicitly visible in review.
- `set_bounds`: `entry`, `exit`, `loop`, `loopStart`, `loopEnd`.
- `set_tempo`: `tempo`, `beatsPerBar`. Changes musical grid, not playback speed.
- `add_cue`: `name`, `start` in seconds.
- `remove_cue`: cue ID in `clipId`.

Timing uses seconds and linear gain uses 0–1, corresponding to silence through 0 dB. The model validates source bounds, repeat, fades, loop ranges and stable identities before a plan can become pending. Up to 64 operations, 32 resulting tracks, 256 clips, 128 cues and a 24-hour exit are supported per plan. Existing playback keeps its snapshot; restart audition after approved authoring changes. No playback is triggered by MCP.

This endpoint edits an existing version-1 MusicArrangement. Create an arrangement in Event Editor first; legacy MusicTracks are never converted automatically. File attachments still use the separate sound/music import tools, and timeline clips reference imported project audio. Parameter curve/routing edits, stinger scheduling, arbitrary code hooks, live overrides and arrangement creation are not part of this endpoint.

## Deterministic client

From the companion directory:

```sh
node client.mjs /absolute/UnityProject inspect
node client.mjs /absolute/UnityProject inspect ARRANGEMENT_GUID
node client.mjs /absolute/UnityProject timeline /absolute/proposal.json
node client.mjs /absolute/UnityProject review PLAN_ID
node client.mjs /absolute/UnityProject status PLAN_ID
```

An illustrative proposal, with identities/hash replaced by inspected values:

```json
{
  "requestId": "a1b2c3d4e5f60123456789abcdef012345",
  "intent": "Delay the percussion entrance and soften its edges",
  "arrangementGuid": "0123456789abcdef0123456789abcdef",
  "expectedHash": "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
  "edits": [{
    "operation": "edit_clip", "clipId": "inspected-stable-clip-id",
    "start": 0.5, "trim": 0.25, "duration": 6,
    "fadeIn": 0.25, "fadeOut": 0.5, "gain": 0.8, "repeat": true
  }]
}
```

## Studio 54 verification

381/381 EditMode tests and 8/8 companion SDK tests passed on the tested revision. Coverage includes immutable proposals, unchanged assets before approval, stale inspect hashes, changed importer metadata, dirty dependencies, invalid timing/IDs, retained references/curves, duplicate/remove operations, persistence and Undo/Redo. Internal application is exercised only on disposable test assets; real proposal approval remains human-only.

The separate BAD SHIFT fixture accepted a live stdio timeline proposal, opened review through the bridge and returned pending status. Target bytes remained unchanged. This verifies the agent-to-editor pending-review path, not a human application or listening acceptance. The 1366×768 arrangement Deck has an opening/rendering regression test. A fresh visually inspected screenshot of the second native Unity instance was unavailable because computer-use routing selected the source Editor instead. No new native player build or listening pass was performed.
