# Agent-assisted audio implementation

Status: product requirement, updated 2026-11-07. The first Editor-only brief,
metadata/library analysis and reviewed-profile workflow is implemented; see
[project-setup.md](project-setup.md). Reviewed native mixer-template installation,
asset routing and VO ducking are implemented in [mix-setup.md](mix-setup.md).
The remaining work below is planned. No AudioMW MCP server or live spectral adviser is implemented. The existing middleware remains
usable without an agent, cloud account or subscription.

See [the 2026-11-10 source and target-profile audit](agent-and-mix-audit-2026-11-10.md)
for current implementation boundaries, transport recommendation and sourced
loudness-profile conditions.

## Customer workflow

A developer gives their preferred coding agent a folder of music and sound files
and asks it to integrate them into a Unity game. AudioMW inventories the actual
project and content, asks for missing intent, prepares a concrete plan, and waits
for human approval before changing project content or audio settings. Monitoring
can report issues and propose corrections; it never silently adjusts the mix.
The same planning and approval UI must work without an agent.

Initial client targets are Claude Code, Codex and Cursor through one optional
AudioMW MCP bridge. Other MCP clients are candidates, not advertised support until
an end-to-end test passes. Unreal is a later engine adapter, not part of the first
Unity implementation. No provider API key or model SDK belongs in the player.
The user's chosen agent may have its own usage costs and data policy.

## Project brief

Read available Unity/project settings first and ask only what remains unknown.
Present a short initial brief with optional advanced details:

| Topic | Required intent or detected context |
| --- | --- |
| Game | Genre, camera perspective, core actions, current integration stage |
| Audience | Target audience, dialogue intelligibility, sensory/accessibility preferences |
| Platforms | Primary and additional platforms, minimum devices, headphones/speakers/TV use |
| Mix | Desired dynamics, references, dialogue priority, user volume categories, explicit delivery targets if any |
| Budget | Voice/CPU/memory constraints, loading/streaming needs, latency-sensitive actions |
| Content | File roles, stems/BPM/loop regions, language, mono/stereo, redistribution rights |
| Project ownership | Existing mixer/groups/effects, audio settings, game event hooks, changes permitted in this batch |

Save the approved brief as a versioned ScriptableObject. Keep detected facts,
user answers, recommendations and unknown values distinct. Audience is design
context, not a basis for inventing a technical loudness requirement. File names
suggest roles but do not prove them. Ambiguous roles and gameplay hooks need
confirmation. Receiving an attachment in a chat does not guarantee that Unity can
read it; resolve and show accessible local files before planning an import.

## Plan, review, apply

1. Inventory files, current audio assets, routing and known gameplay hooks.
2. Analyse readable audio and propose categories, events and musical structure.
3. Build a versioned change plan with exact created/modified assets, previous and
   proposed values, reasons, evidence, assumptions and validation steps.
4. Show one review in Unity: brief summary, expandable changes, Apply selected,
   Reject, and Why. A user may approve a meaningful batch rather than every file.
5. Apply the approved immutable plan, validate it, and report actual results.
6. Offer subsequent recommendations separately; each project mutation needs a
   fresh approval. Rejecting a recommendation leaves authored values untouched.

Planning may read files and write a clearly scoped non-Assets analysis cache;
it must not import clips, create assets, modify importers or trigger playback.
An A/B preview is a separate explicit user action and restores temporary state.
An unanswered question is never approval. Rejection keeps a draft available for
revision without adding repeated unsolicited prompts.

Example review, with illustrative values rather than universal presets:

> Create Music, SFX, Ambience, VO and UI groups and route 24 new events.
> During VO, reduce Music by 6 dB with a 100 ms attack and 400 ms release.
> Why: keep dialogue clear while retaining musical continuity.
> Apply / Reject / Why

For a technical problem, state the observation first: "The captured combat mix
exceeds your approved peak ceiling during simultaneous explosions. I propose
reducing the explosion bus by 2 dB. This leaves more headroom." Show the measured
value, selected ceiling, capture scope and audition action in expanded details.
Never present an unheard proposed change as a proven audible improvement.

## Routing and processing

Propose a small initial hierarchy: Master with Music, SFX, Ambience, VO and UI.
Reuse compatible existing groups by explicit mapping; preserve existing routing,
user volume settings and third-party integrations. More groups need a concrete
workflow reason. Events, music, VO and scene/game hooks belong in the same plan
so that creating assets alone cannot be reported as completed implementation.

Create useful processing only when justified: dialogue ducking, an approved
safety stage, or selected reverb/filter routes. Do not populate every bus with
EQ, compression and limiting. Detect available effects and platform support;
never silently install, purchase or depend on third-party plugins. Unsupported
processing is an explicit unresolved item.

Mixer construction requires an early compatibility spike. Prefer a shipped,
validated mixer template and supported Unity APIs. Do not promise arbitrary
AudioMixer graph/effect authoring before verifying the available API on Unity
2022.3 and Unity 6. No brittle mixer-YAML surgery or private reflection workaround.

## Platform profiles, loudness and spectral advice

AudioTierConfig already expresses voice, prewarm, buffer, sample-rate and DSP
budgets. MixerRoutingProfile already binds parameters to exposed mixer values.
Reuse those concepts rather than adding a parallel runtime configuration system.
A selected platform proposes budgets and importer policies; the review shows
memory/CPU/latency tradeoffs and exact affected files. Do not silently force an
OS output rate or overwrite a project's audio configuration.

Platform alone does not determine master gain or a universal LUFS target. Store
an explicit project mix target, peak ceiling, dynamics intent and reference
capture procedure. Distinguish source-clip loudness, bus loudness, rendered master
measurements and physical listening level. A clip library audit cannot certify
the final game mix. No automatic gain chasing, per-frame normalization or promise
of calibrated speaker SPL from digital measurements.

The existing BS.1770 clip meter and true-peak estimator are a starting point, not
proof of an implemented live master monitor. Develop opt-in capture/analysis with
bounded CPU/memory cost. Report clipping/headroom, excessive overlaps, missing
routing, failed duck recovery, mono compatibility and selected target deviations
with reproducible captures. A true-peak ceiling needs appropriately measured
output; a generic compressor is not a guaranteed true-peak limiter.

Treat frequency-balance and masking analysis as suggestions with measurement
context. Identify possible music/VO masking or excessive low-frequency energy,
then offer auditionable gain/EQ/duck changes with uncertainty stated. Do not EQ
content solely from genre, audience or platform, and do not claim to measure an
unknown headphone/speaker response from an in-engine spectrum.

## Agent boundary and approval ownership

Expose narrow typed tools for project/content inspection, analysis, plan drafting,
plan validation, review requests and result retrieval. A local stdio MCP bridge
is the first transport candidate; keep the domain plan format independent of the
transport and engine. Unity executes editor operations on its main thread.
No agent-facing arbitrary C# evaluator, arbitrary shell command or raw serialized
property writer is part of the AudioMW interface.

Approval is owned by the Unity review UI, not a tool argument such as
approved=true, a model-written chat message or the client's auto-approve setting.
The local executor applies the selected immutable plan after that user action;
the agent reads the result. There is no MCP approve endpoint. The authorization
is bound to the project identity, plan digest and captured source revisions.
Modified inputs, project switching, editor reload or expired authorization require
a new review. Multiple Unity instances cannot share an implicit active target.

Apply operations are idempotent and journal created/modified assets. Preflight
checks include unsaved scenes, current play/compile state and conflicting edits.
Use Unity Undo where supported plus a persisted change journal. Do not promise
transactional rollback of arbitrary importer/code/mixer changes: each supported
operation needs a tested compensation path. A failed operation reports exactly
what changed; conflict recovery must preserve subsequent user edits. Retrying
cannot create duplicate groups, events or hooks.

The boundary protects AudioMW's own tools; it cannot prevent an independently
authorized coding agent from editing project files with other tools. Keep that
limitation explicit. AudioMW does not upload audio to a vendor service. External
agent attachments and provider policies remain under the user's control.

## Implementation order and acceptance

| Batch | Deliverable | Evidence required before advertising |
| --- | --- | --- |
| A: onboarding and review | Project brief, read-only inventory, deterministic plan/diff and Unity review UI; usable without AI | No project writes before approval; reject, stale-input and wrong-project cases; native narrow/light/dark/keyboard QA |
| B: approved integration | Import/event creation, validated mixer template/mapping, VO duck proposal and known gameplay hooks | Relay Field integration from supplied files; exact diff; rerun without duplicates; conflict/rollback tests; installed package tests on both editor versions |
| C: agent bridge | Optional MCP tools wrapping the same planner/executor, install instructions | Separate Claude Code, Codex and Cursor happy/reject/stale-plan/multi-instance tests; a client auto-approval cannot approve a Unity plan |
| D: mix adviser | Opt-in rendered-mix capture, measured warnings, auditionable recommendations | Bounded profiler results; known test signals; user target checks; duck recovery; manual A/B listening; no background changes |

Start with A and a small B vertical slice on Relay Field: selected footsteps,
Music/SFX/VO routing, one known pickup hook and a proposed VO duck. Keep detection
and generation deterministic where possible. An agent supplies intent and handles
ambiguity; measured diagnostics and the validated executor determine what can
actually be applied. Add broader code adapters and spectral recommendations only
after the review/approval loop has been proven.

This is an optional authoring workflow in the existing one-time product concept.
It is not evidence of greater willingness to pay or justification for a price
increase without pilot use. Existing listening, content-rights and platform
release gates remain open; an unshipped adviser is not an Asset Store claim.

## Primary references checked 2026-11-07

- [MCP tools and human review](https://modelcontextprotocol.io/specification/2025-06-18/server/tools): a shared tool protocol does not itself enforce the product approval policy.
- [Claude Code MCP](https://code.claude.com/docs/en/mcp): local stdio servers are supported.
- [OpenAI Docs: MCP in local Codex clients](https://learn.chatgpt.com/docs/extend/mcp?surface=cli): client configuration supports local stdio and remote HTTP servers.
- [Cursor MCP](https://prod.cursor.com/docs/mcp): external tools and local stdio are supported; client approval defaults are not the AudioMW approval boundary.

Client transport support is not evidence that this proposed AudioMW integration
already works with any client. Pin the client versions in eventual test reports.
