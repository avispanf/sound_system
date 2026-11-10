# Reviewed adaptive music imports — preview.18

The optional local MCP companion now exposes `audiomw_propose_music`. Connect it using [agent-mcp.md](agent-mcp.md). Files must be readable local WAV/OGG/AIFF attachments. The adapter has no approval or playback command. Enable Agent Review in the intended Unity editor session, inspect first, propose, show review, then wait for the developer's Unity confirmation.

## What changes

A proposal imports 1–8 MusicTracks, with optional intro/body and up to 16 synchronized stems per track. A looping body or at least one stem is required. Supply BPM (10–400), beats per bar (1–16), each stem's role, and its existing SoundParameter GUID when adaptive. Weight endpoints map the parameter's normalized minimum/maximum to 0–1 gain, with a 0–30 second fade. Constant stems use equal endpoints without a parameter.

An optional MusicStateProfile maps state names to distinct proposed tracks. Transitions use Immediate, Beat or Bar, Linear or EqualPower, and 0–30 second crossfades. Rules and defaults are explicit in the review. Replacing a profile retains its GUID and must keep all existing state names, so current gameplay requests remain addressable. The complete reviewed state/rule set replaces the previous set.

Track replacement preserves GUID, scene/profile references, mixer route and volume. It replaces the complete intro/body/layer set, BPM and meter, and enables looping. Existing markers are deliberately rejected: marker-authored arrangements require the specialist editor. A new track requires an explicit existing mixer-group ID and starts at 0 dB. New profiles and tracks are not automatically bound to gameplay. Existing BAD SHIFT Explore/Combat references work without scene changes.

Limits remain 64 files, 128 MB per file and 512 MB per plan. Proposal preparation writes no imported Assets. Imported audio is decoded after approval; all body/stem clips must have the same sample rate and duration (one-sample tolerance), with whole bars at the proposed BPM (two-sample tolerance). Intros must span whole bars too. Wrong timing fails and rolls back instead of silently trimming, resampling or time-stretching. Musical phase/transient alignment and subjective loop quality still need human listening.

## Example request

Use GUIDs from `audiomw_inspect`, which now includes Tracks, Profiles and Parameters alongside events, groups and project briefs. Generate a fresh requestId for a different proposal; retries use the same ID and identical content.

```json
{
  "requestId": "0123456789abcdef0123456789abcdef",
  "intent": "Replace Explore music with supplied synchronized layers",
  "destination": "Assets/Audio",
  "tracks": [{
    "name": "Explore",
    "purpose": "Existing exploration state",
    "replaceTrackGuid": "GUID_FROM_INSPECT",
    "tempo": 120,
    "beatsPerBar": 4,
    "body": "/absolute/explore-bed.wav",
    "forceToMono": false,
    "stems": [{
      "name": "Pulse",
      "file": "/absolute/explore-pulse.wav",
      "parameterGuid": "PARAMETER_GUID_FROM_INSPECT",
      "lowWeight": 0,
      "highWeight": 0.7,
      "fadeSeconds": 0.25,
      "forceToMono": false
    }]
  }]
}
```

For a state profile, add `profile` with `name`, optional `replaceProfileGuid`, `states: [{"name":"Explore","track":"Explore"}, {"name":"Combat","track":"Combat"}]`, `quantization`, `fadeSeconds`, `curve`, and `transitions: [{"from":"Explore","to":"Combat","quantization":"Bar","fadeSeconds":0.5,"curve":"EqualPower"}]`. Both referenced tracks must be in the same proposal. Omitted transitions default to an empty list; default transition settings still apply.

Equivalent SDK CLI: `node client.mjs /absolute/UnityProject music /absolute/proposal.json`. Review and status use the existing commands. Reconnect the MCP client to refresh its cached tool list after updating the companion.

## Mono and stereo

`forceToMono: false` means preserve source channel layout. `true` enables Unity's native Force To Mono importer setting on the new imported copy; the original attachment remains unchanged. The track-level flag covers intro/body; each stem has its own flag. The review displays each choice.

After import, select the AudioClip in Unity's native Inspector, change Force To Mono and Apply to reimport the same asset. Disabling it restores stereo only if the underlying source is stereo; it does not manufacture stereo from a mono recording. AudioMW Browser stays read-only. This MCP batch does not add a separate tool for changing existing AudioImporter settings.

A real stereo fixture was tested through stereo → mono → stereo reimport, checking channel count, differing left/right samples, unchanged source bytes, clip GUID, sample count and MusicTrack references. This is channel-layout verification, not a listening or downmix loudness certification. Unity's own mono normalization setting still governs its downmix; AudioMW does not perform LUFS normalization.

[Unity Force To Mono API](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/AudioImporter-forceToMono.html)

## Preservation and limits

Source hashes, track/profile/parameter snapshots, mixer state, project, session, build target and destination are checked before applying. Music targets are checked again after import. All timing checks precede track/profile writes; caught failures restore touched existing assets and remove the import folder. The existing interrupted-operation/receipt limitations and local-user trust boundary still apply.

VoiceLine authoring, scene/code hook generation, marker transitions, tempo automation, arbitrary multi-point curves, live LUFS and Unreal are outside this batch. Native Unity confirmation remains the only supported approval path.
