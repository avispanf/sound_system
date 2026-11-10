# Local agent integration — preview.18

AudioMW now has an optional local stdio MCP adapter for Claude Code and Codex. It proposes local audio imports, SoundEvent changes and adaptive music arrangements. See [agent-music-mcp.md](agent-music-mcp.md) for stems, musical states and mono/stereo behavior. The Unity Agent Review window is the only approval path exposed by this integration.

## Install and connect

Requires Node.js 20 or newer, the AudioMW editor package from this revision, and the Tools/AgentBridge companion directory. UPM archives also include it as AgentBridge~; copy that folder outside Unity's package cache before installing Node dependencies. In that directory run `npm ci --ignore-scripts`. No hosted service, API key or model is embedded in Unity. Keep the lockfile; this preview uses the official MCP TypeScript SDK 1.32.2.

Open the intended Unity project, stay in Edit Mode, then Window > AudioMW > Agent Review > Enable local agent bridge. The bridge expires when the editor reloads scripts or closes; enable it again after compilation. This explicit session opt-in also prevents agents from choosing another running Unity project.

Configure clients using absolute paths, quoting paths containing spaces:

```sh
claude mcp add --scope local --transport stdio audiomw -- node /absolute/Tools/AgentBridge/server.mjs --project /absolute/UnityProject
codex mcp add audiomw -- node /absolute/Tools/AgentBridge/server.mjs --project /absolute/UnityProject
```

Run the Claude command from the Unity project directory. Codex's command creates a user-level named server pinned to that explicit project. Use a different name for each project. These commands do not replace existing client configuration. Restart/reconnect the client if its current conversation has already cached its tool list. A configured/connected MCP process is not proof that the Unity editor bridge is enabled; `audiomw_inspect` checks the live heartbeat.

The prepared BAD SHIFT setup on this development machine uses `audiomw-bad-shift`. Paths in local client configuration are machine-specific and must not be copied to another machine unchanged.

## Chat workflow

1. Add audio files to the chat or give their local paths. The client must resolve attachments to readable absolute local files. A cloud attachment ID or URL is not a filesystem path; this preview does not fetch URLs or receive binary uploads.
2. Ask the agent to inspect the intended project. It reads existing SoundEvents, mixer group IDs and saved project briefs. If roles or gameplay intent are unclear, the agent asks; a filename alone is not an authoritative classification.
3. The agent sends an explicit proposal: purpose, destination folder, files grouped by event, and either an existing event GUID or an existing mixer group for a new event. Proposal IDs provide retry idempotency within the editor session.
4. The agent opens Agent Review. Unity shows source and destination paths, file hashes/sizes, existing clips, preserved playback settings, routing, purpose and limitations. Approve and apply opens a final Unity confirmation. Reject changes no Assets.
5. The agent polls plan status. Applied means Unity decoded the clips and persisted the reviewed event changes. Verify the actual gameplay action and listen before accepting audio quality.

Example prompt:

> Inspect BAD SHIFT with AudioMW. Use these two attached WAV files as variations for the existing MicrowaveRandom event triggered by E. Preserve its routing, volume, selection and scene references. Prepare the integration plan and show it in Unity. Wait for me to approve there, then read the result. Do not play audio during automated verification.

Tools: `audiomw_inspect`, `audiomw_propose`, `audiomw_propose_music`, `audiomw_show_review`, `audiomw_plan_status`. There is no approval, arbitrary C#, shell, code-generation or remote-play tool. For an equivalent deterministic SDK client:

```sh
node client.mjs /absolute/UnityProject inspect
node client.mjs /absolute/UnityProject propose /absolute/proposal.json
node client.mjs /absolute/UnityProject review PLAN_ID
node client.mjs /absolute/UnityProject status PLAN_ID
```

Proposal shape:

```json
{
  "requestId": "0123456789abcdef0123456789abcdef",
  "intent": "Replace the microwave variations",
  "destination": "Assets/Audio",
  "events": [{
    "name": "Microwave",
    "purpose": "Existing E interaction",
    "files": ["/absolute/microwave-a.wav", "/absolute/microwave-b.wav"],
    "replaceEventGuid": "GUID_FROM_INSPECT"
  }]
}
```

Generate a new 32-character lowercase hex request ID for a new proposal. Reuse the ID only with identical input after a timeout. Status queries use the plan ID returned by Unity, which is distinct from the request ID. A restart invalidates pending plans and cached requests; run inspect and propose again.

## Scope and preservation

- WAV, OGG and AIFF; 1–16 events, up to 32 clips per event and 64 per plan; 128 MB per clip and 512 MB total. Unity performs actual decode validation during approved import.
- Replacements preserve event GUID, existing references, mixer route, gain, selection, spatial settings and looping. Only direct clip lists in Simple containers are supported; weighted selection, child containers and addressed references are rejected.
- New events require an explicit mixer group, use RandomNoRepeat, 3D logarithmic attenuation, 0 dB event gain and no loop. Their gameplay hooks remain unbound. The review states this prominently.
- Destination must be an existing folder under Assets. Each plan creates its own import subfolder; existing files are never overwritten. Source hashes, target GUID/content, mixer dependency state, project/session and build target are rechecked at Apply. Unsaved target edits block application.
- Staged copies are hashed before import. All clips must decode before events change. A caught apply failure restores touched existing events and removes the created import folder. Receipts with the reviewed plan live under Library/AudioMWAgent/receipts; an interrupted editor/process can leave a pending receipt or partial import that needs inspection. This is not a cross-process crash-atomic asset transaction or a Git commit.
- Normal operations do not alter mixer gains, platform settings, scene/code hooks or VoiceLines. MusicTracks and state profiles use the separate reviewed music proposal. The existing Project Setup and Mix Setup windows handle those separate reviewed workflows. Continuous master LUFS, automatic platform presets and Unreal remain future work.

## Local trust boundary

The MCP process communicates through a bounded, polled mailbox in the selected project's Library folder. No TCP/HTTP listener or public endpoint is opened. A heartbeat, fresh session ID and project identity prevent accidental stale/wrong-project execution. Symlink mailbox/destination paths are rejected; attachment paths are resolved to real local files by the adapter.

The session ID is not a security boundary against software with arbitrary write access to the same user's files. An agent with unrelated shell/Unity-evaluation tools could bypass any editor workflow; do not advertise this bridge as sandboxing those tools. The supported AudioMW tools cannot approve. Their server instructions explicitly forbid approval through alternate tools. All content remains local to the bridge, but a chat client can send returned names, paths, project brief and metadata to its configured model provider.

## Official client references

- [Codex MCP configuration](https://learn.chatgpt.com/docs/extend/mcp?surface=cli)
- [Claude Code MCP configuration](https://code.claude.com/docs/en/mcp)
- [Official TypeScript SDK](https://github.com/modelcontextprotocol/typescript-sdk/tree/v1.x)

See [agent-mcp-verification.md](agent-mcp-verification.md) for observed acceptance results and remaining limits.
