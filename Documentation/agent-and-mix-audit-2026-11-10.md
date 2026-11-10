# AudioMW agent, UI and mix-target audit — 2026-11-10

Later implementation update: preview.17 adds the local MCP companion and reviewed SoundEvent imports. See [agent-mcp.md](agent-mcp.md) and [acceptance results](agent-mcp-verification.md). The audit below records the earlier baseline; its statements about an absent bridge are superseded for that limited scope. Master metering, adviser and Unreal gaps remain open.

This is a source audit and implementation proposal, not a claim that the agent bridge or Unreal adapter has shipped. The current BAD SHIFT demo is a separate gameplay/visual showcase. Its live audio map reads runtime events and mixer values; it does not measure final-output LUFS.

## What works today

| Area | Verified in current source | Remaining boundary |
| --- | --- | --- |
| Project questionnaire | AudioProjectSetupWindow, AudioProjectBrief: game, genre/actions, audience, platforms, perspective, devices, mix intent, delivery target, content/rights, voice/memory budgets | User opens Project Setup; no first-install guided onboarding and no structured target/preset selection |
| Analysis/review | AudioProjectPlanner: inventory, library issues, immutable digest, source/destination checks, Why and rejection | Role inference, external file import and gameplay binding are not implemented by this planner |
| Mixer installation | AudioMixPlanner and Mix Setup: native template, category routing, VO attack/release, reviewed apply, receipts and conflict handling | No universal master normalization, arbitrary mixer-effect graph generation, automatic genre preset or third-party plugin installation |
| Clip loudness | LoudnessMeter and Loudness Audit: integrated, momentary/short-term maxima, sample/true-peak estimates | Offline clip analysis; no continuous rendered-master monitor, no session coverage or compliance certificate |
| Agent integration | Public editor planning/execution code can be used in controlled development fixtures; this environment has a separate Unity MCP toolset | No AudioMW MCP server, client configuration package or end-to-end Claude/Codex/Cursor acceptance evidence |
| Unreal | Requirement only | No Unreal runtime/editor adapter exists in this package |

## Transport recommendation

Use a typed AudioMW authoring API and versioned plan/result schema as the core. Add a small optional local stdio MCP adapter for Claude Code, Codex and Cursor. A CLI that reads/writes the same JSON is a useful deterministic test/CI entry point; a skill/plugin supplies workflow instructions. Neither replaces the API or grants approval. Avoid one bespoke implementation per assistant, a mandatory hosted service or an embedded model in the game.

Claude Code documents local stdio servers; local Codex clients document stdio and Streamable HTTP. Those client capabilities do not prove AudioMW connectivity. There is currently no install command for an AudioMW bridge to give users. Do not invent a package name or configure an unimplemented server.

Proposed operations: project identity/capabilities, inspect content, analyse supplied local files, draft brief, propose import/event/hook plan, validate plan, show Unity review, retrieve apply/validation result. The adapter must not expose arbitrary shell/C# evaluation or an approval endpoint. Bind every request to an explicit project and editor session, including multiple-editor handling. A changed plan or source snapshot invalidates review.

Attachments must first resolve to accessible local files with hashes. Copy/import only after the developer sees the target paths, role assignments and changes in Unity. A file named footstep is a hint, not proof of its role. Loop points, stems/BPM and gameplay hooks need actual metadata or developer input. Success means events fire from the intended game actions, not merely that .asset files were created.

## Questionnaire and proposed profile model

Ask only missing questions; infer build target and existing routing from Unity. Keep facts, answers, suggested defaults and unknowns separate.

1. Platforms, minimum devices, headphones/TV/onboard speakers and mono requirements.
2. Genre, camera, typical and busiest gameplay, rhythm/latency sensitivity.
3. Dialogue priority, subtitles/accessibility, desired dynamic range and reference audio.
4. Delivery specification or explicitly chosen loudness profile and version; representative measurement route.
5. Voice, memory, streaming and CPU budgets; existing mixer, effects and hooks to preserve.
6. File roles, languages, source rights, stems/tempo and permitted changes.

The proposal is composed from independent profiles: performance tier, listening/dynamics intent, content roles and a measured-mix target. Platform is not a direct mapping to master gain. Proposed gains are shown in a diff, applied once after Unity approval and followed by a new capture. Background advice never silently changes the mix or follows LUFS with continuous gain correction.

### Existing performance starting points

These values are read from AudioTierConfig factories, not measured platform requirements. Applying them can reset Unity audio configuration; the review must show this explicitly and offer retaining the current device rate.

| Factory | Pool / real voices | Sample rate | DSP buffer | Notes |
| --- | --- | --- | --- | --- |
| Mobile2D | 16 / 12 | 24000 Hz | 1024 samples | Spatial, occlusion and custom DSP disabled; not a universal mobile preset |
| Standard3D | 32 / 32 | 48000 Hz | 512 samples | Starting 3D profile |
| HighEnd | 96 / 64 | 48000 Hz | 256 samples | 24 prewarmed voices; profile target hardware |

The current reviewed mix request defaults to Music −6 dB and Ambience −6 dB during VO, 100 ms attack and 400 ms release. Keep these as editable audition starting values, not automatic genre or platform rules. Master and category defaults remain at the authored template values.

Genre proposals are creative, not standards: dialogue-led games emphasize VO priority and auditioned ducking; action games prioritize important transients and overlap budgets; horror preserves intentional quiet/loud contrast; rhythm games require device-latency testing and must avoid unreviewed timing/processing changes. Begin category/master faders at the authored template values; determine corrections from real content and a representative mix capture.

### Loudness reference to retain

ITU-T H.872 (10/2024), clause 9.3.1, recommends a 30-minute integrated gameplay window around −23 LUFS with ±2 LU tolerance and true peak not above −1 dBTP. Its portable-speaker exception is −18 LUFS ±2 when onboard speakers lack the dynamic range for the main target, with a recommended 5 dB headphone reduction. This is an optional reference profile, not a Unity/Unreal store rule or a per-SFX normalization target. Genre does not change these numbers by itself. Explicit platform delivery requirements can take precedence; store their source/version with the selected profile.

Measure rendered output at a documented tap point with channel layout, sample rate, duration, user/category/master gain states and scenario coverage. Show insufficient coverage instead of Pass for a short capture. Keep digital loudness separate from acoustic listening level. Existing clip-meter estimates need streaming/state/channel-layout validation before being reused for a master monitor.

## Proposed adviser delivery

First implement opt-in bounded PCM capture and a streaming meter: momentary, short-term, gated integrated loudness, true peak, elapsed/covered duration. Avoid allocations, file I/O and full-buffer analysis on the audio thread. Define whether the tap is before or after user master volume and capture that in the report. Validate known signals, silence, discontinuities, pause, scene reload, device changes and target channel layouts. Keep capture local by default.

Then add reproducible recommendations: peak ceiling exceeded, sustained target deviation, VO duck failing to recover, missing routes, rejected/virtualized important events and excess overlaps. Each card needs observation, capture interval, confidence, concise Why, proposed diff, audition and Apply/Reject in Unity. Spectral masking/EQ is later advice requiring A/B listening, not an automatic correction from genre or audience.

Acceptance for the agent bridge: Claude Code and Codex separately complete import-to-gameplay verification; reject, stale plan, missing attachment, editor restart, wrong project and retry/idempotency cases pass. Add Cursor independently before advertising it. Unreal reuses schemas and recommendations but needs its own editor executor, native asset mapping, mixer/submix capture and tests.

## Design 02 comparison

Checked against the supplied CODEX-HANDOFF.md and current editor source. This is a code-level comparison; this turn does not certify every native window width/theme or all keyboard interactions.

| Handoff contract | Current evidence |
| --- | --- |
| Read-only Browser; native Inspector editing | AudioBrowserWindow and SoundEventInspector retain the separation |
| One-click selection, explicit pin/follow, persisted GUID/filter/split | Serialized Browser state, native focused Inspector and empty-filter reset present |
| Source preview distinct from event audition | AudioSourcePreviewPane labels original-file playback; event audition directs to actual Play Mode |
| Contextual containers, dB editing, foldout summaries, Undo | Existing Inspector/DecibelGain code, serialized properties and tests |
| Diagnostics Event/Outcome/Reason and captured gains | EventDebuggerWindow has all three columns, rejection filter and captured gain detail |
| Music Current/Pending, timing/curve and cancel only pending | MusicStatePreviewWindow reads runtime pending state and limits cancellation before its scheduled start |
| Native light/dark styling, resizable layout | UI Toolkit and Unity theme variables; historical narrow/theme QA documented in sound-event-workflow.md |
| All visuals identical to HTML or diorama reference | Not claimed: HTML is interaction direction; gameplay diorama intentionally adds physics, combat and the requested new bathroom layout |

The old sound-event-workflow text that called the Diagnostics table and Music refinements future work is stale: both now exist in source. Remaining gaps are the agent/onboarding/adviser workflows above and renewed native QA after future UI changes, not justification to replace the existing editor with a second mockup UI.

## Primary sources checked today

- [Codex MCP configuration and supported transports](https://learn.chatgpt.com/docs/extend/mcp?surface=cli)
- [Claude Code MCP](https://code.claude.com/docs/en/mcp)
- [MCP tool schemas and interaction model](https://modelcontextprotocol.io/specification/2025-06-18/server/tools)
- [ITU-T H.872 official PDF, clause 9.3.1](https://www.itu.int/rec/dologin_pub.asp?id=T-REC-H.872-202410-I!!PDF-E&lang=e&type=items)

These establish client/standard capabilities and reference recommendations, not certification of AudioMW. The optional bridge, structured presets and live adviser remain separate implementation batches after this demo handoff.
