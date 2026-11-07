# Project Setup: brief, analysis and reviewed plan

The first assisted-authoring stage is implemented as an Editor-only workflow.
Open **Window > AudioMW > Project Setup**, or **Tools > Project setup** from an
AudioMW workspace. This works without an AI client or MCP connection.

## Use

1. Describe the game, genre/core actions, target audience and platforms. Game name
   and current build target are detected when starting an empty brief. Change the
   detected defaults if the shipping targets differ.
2. Choose an existing content folder under Assets. Expand Mix, devices and budgets
   for camera, listening devices, dialogue/mix intent, delivery target, content
   rights notes and requested voice/memory budgets. No platform loudness target is
   invented. Budgets are user intent, not measured usage.
3. Choose an .asset destination in an existing Assets folder. Analyse and prepare
   review reads imported clips and AudioMW assets in the content folder, plus
   mixers and banks elsewhere under Assets. It captures clip channels/rate/length/
   load type, event structure, tiers, mixer groups and existing library diagnostics.
4. Inspect the inventory and expand Why for short recommendation explanations.
   Reject retains the draft and writes no assets. Editing any brief answer clears
   the review and disables approval until analysis is repeated.
5. Approve and save plan creates or updates one AudioProjectSetup asset containing
   the brief, reviewed snapshot, source fingerprint and revision. Existing event,
   mixer, importer, gain and gameplay configuration is unchanged. Repeating an
   identical reviewed snapshot does not add another revision; changed
   recommendations require a newly saved revision after approval.
6. Select a saved setup profile and Open Project Setup, or use Load setup in the
   window. A loaded profile supplies a draft; it does not restore live approval.

The only executable change in this stage is saving the setup profile. Routing,
VO ducking, file roles, mix capture and platform/device validation are advisory
next-stage work, not applied operations. Saving recommendations is not permission
for a future implementation batch. External-file import, gameplay-hook discovery,
MCP, rendered-mix metering and spectral analysis remain unimplemented.

## Review integrity

A plan captures project identity, editor version, active build target, exact brief,
audio dependency hashes, AudioManager/ProjectSettings contents and the destination
asset snapshot. Saving checks those inputs again. Changed/added/removed audio,
unsaved audio/profile edits, a changed destination, another project or edited
answers require a new review. A destination belonging to another asset type is
never overwritten. Use Edit Mode after import/compilation completes; unsaved scene
edits block approval so that the workflow cannot silently save them.

The window retains draft fields through normal Unity serialization but does not
serialize a live plan/approval. Rebuilding the view after reload requires analysis
again. The plan's public read method returns a copy, so changing the displayed
snapshot cannot change the approved operation. The UI invokes the executor; no
MCP approve endpoint or client integration exists in this stage. This is not a
security boundary against separately authorized code/file-editing tools.

Profile creation and updates use Unity Undo. Saving targets only the affected
profile, preserving unrelated unsaved assets. The profile Inspector summarizes
what was saved and directs further editing back to Project Setup. Normal Unity
Undo/save behavior applies; broad importer/code/mixer rollback is not claimed.

## Evidence and remaining QA

Twelve new EditMode cases exercise read-only inventory, immutable plan reads,
brief and audio changes, unsaved/conflicting destinations, safe paths, required
intent/budgets, profile persistence/revisions, rejection, real attached UI change
callbacks and view-rebuild invalidation. These window tests require a graphics
context, as do existing authoring UI tests; run the full suite without -nographics.

Tools/ManualPlayground/Editor/ValidateProjectSetup.cs runs a guarded Relay Field
fixture check: analysis and rejection do not create a profile, changed answers
invalidate the plan, explicit test approval saves the profile, and existing game
content remains byte-identical. It writes TestReports/project-setup-review.json
and project-setup-validation.json. It refuses to replace an existing setup
profile; run it only on a fresh generated fixture. OpenReview opens the prepared
profile draft for native inspection in the marked playground project.

Native Unity 6 dark-theme checks cover analysis, edit invalidation, rejection,
Why and save at 620-point width; narrow 380-point layout is checked separately.
Light-theme, broader accessibility, large-library responsiveness and MCP clients
remain separate gates. Clip metadata/library diagnostics do not establish
loudness compliance, rendered frequency balance or sound quality by ear.

## Next executable review

The Configure routing and ducking action opens Mix Setup. It is a separate explicit
approval, described in [mix-setup.md](mix-setup.md); saving this brief alone still
does not change audio routes or settings.
