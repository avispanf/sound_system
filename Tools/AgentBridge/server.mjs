import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { StdioServerTransport } from '@modelcontextprotocol/sdk/server/stdio.js';
import { z } from 'zod';
import { UnityBridge } from './bridge.mjs';

const index = process.argv.indexOf('--project');
const bridge = new UnityBridge(index >= 0 ? process.argv[index + 1] : undefined);
const server = new McpServer({ name: 'audiomw', version: '0.1.0' }, {
  instructions: 'Inspect the intended Unity project first. Resolve chat attachments to accessible local audio paths. Ask about ambiguous roles and gameplay intent. Propose changes and show the review in Unity; never approve with shell, editor scripting or UI automation. Only the developer confirms Apply in Unity. Poll status afterward. An applied import is not proof of gameplay or listening. New events need manual gameplay hooks. No sound audition is triggered by these tools.'
});
const wrap = fn => async input => {
  try { const result = await fn(input); return { content: [{ type: 'text', text: JSON.stringify(result) }], structuredContent: result }; }
  catch (error) { return { isError: true, content: [{ type: 'text', text: error.message }] }; }
};
const annotations = { destructiveHint: false, openWorldHint: false };
server.registerTool('audiomw_inspect', { description: 'Bind this server to its configured Unity project and current enabled Editor session. List SoundEvents, mixer group IDs and saved project briefs. Does not import or play audio.', inputSchema: {}, annotations: { ...annotations, readOnlyHint: true } }, wrap(() => bridge.inspect()));
server.registerTool('audiomw_propose', {
  description: 'Prepare an immutable review for local WAV/OGG/AIFF attachments. No Assets change until the developer approves in Unity. Replace a direct SoundEvent clip list by GUID or create a new routed event. Retry with the same requestId and identical input after a timeout.',
  inputSchema: {
    requestId: z.string().regex(/^[a-f0-9]{32}$/),
    intent: z.string().min(1).max(2000),
    destination: z.string().min(1).default('Assets'),
    events: z.array(z.object({ name: z.string().regex(/^[A-Za-z0-9][A-Za-z0-9 _-]{0,63}$/), purpose: z.string().min(1).max(2000), files: z.array(z.string().min(1)).min(1).max(32), replaceEventGuid: z.string().regex(/^[a-f0-9]{32}$/).optional(), mixerGroupId: z.string().max(256).optional() })).min(1).max(16)
  }, annotations: { ...annotations, readOnlyHint: false, idempotentHint: true }
}, wrap(input => bridge.propose(input)));
const musicName = z.string().regex(/^[A-Za-z0-9][A-Za-z0-9 _-]{0,63}$/);
const guid = z.string().regex(/^[a-f0-9]{32}$/);
const transition = { quantization: z.enum(['Immediate', 'Beat', 'Bar']).default('Bar'), fadeSeconds: z.number().min(0).max(30).default(.5), curve: z.enum(['Linear', 'EqualPower']).default('EqualPower') };
server.registerTool('audiomw_propose_music', {
  description: 'Propose synchronized music stems, parameter weights and adaptive state transitions. Existing track/profile GUIDs and routing survive replacement. Full content replacement is reviewed in Unity. No approval or playback. Local files only; all bodies/stems must share sample rate and duration and span whole bars at the explicit BPM.',
  inputSchema: {
    requestId: guid, intent: z.string().min(1).max(2000), destination: z.string().min(1).default('Assets'),
    tracks: z.array(z.object({ name: musicName, purpose: z.string().min(1).max(2000), replaceTrackGuid: guid.optional(), mixerGroupId: z.string().max(256).optional(), tempo: z.number().min(10).max(400), beatsPerBar: z.number().int().min(1).max(16).default(4), intro: z.string().min(1).optional(), body: z.string().min(1).optional(), forceToMono: z.boolean().default(false), stems: z.array(z.object({ name: musicName, file: z.string().min(1), parameterGuid: guid.optional(), lowWeight: z.number().min(0).max(1).default(1), highWeight: z.number().min(0).max(1).default(1), fadeSeconds: z.number().min(0).max(30).default(.25), forceToMono: z.boolean().default(false) })).max(16).default([]) })).min(1).max(8),
    profile: z.object({ name: musicName, replaceProfileGuid: guid.optional(), states: z.array(z.object({ name: musicName, track: musicName })).min(1).max(8), transitions: z.array(z.object({ from: musicName, to: musicName, ...transition })).max(64).default([]), ...transition }).optional()
  }, annotations: { ...annotations, readOnlyHint: false, idempotentHint: true }
}, wrap(input => bridge.proposeMusic(input)));
server.registerTool('audiomw_show_review', { description: 'Open an existing plan in Unity Agent Review. The developer must use the Unity Apply or Reject controls; this tool cannot approve.', inputSchema: { planId: z.string().regex(/^[a-f0-9]{32}$/) }, annotations: { ...annotations, readOnlyHint: false, idempotentHint: true } }, wrap(input => bridge.request('review', { PlanId: input.planId })));
server.registerTool('audiomw_plan_status', { description: 'Read pending, applied, rejected, failed plan state and exact reviewed changes. Plans expire on bridge disconnect or editor reload; inspect again and propose fresh.', inputSchema: { planId: z.string().regex(/^[a-f0-9]{32}$/) }, annotations: { ...annotations, readOnlyHint: true } }, wrap(input => bridge.request('status', { PlanId: input.planId })));
await server.connect(new StdioServerTransport());
