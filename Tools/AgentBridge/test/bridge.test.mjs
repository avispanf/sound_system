import test from 'node:test';
import assert from 'node:assert/strict';
import { mkdtemp, mkdir, writeFile, readFile, readdir, unlink, rm, realpath, symlink } from 'node:fs/promises';
import os from 'node:os';
import path from 'node:path';
import { randomBytes } from 'node:crypto';
import { Client } from '@modelcontextprotocol/sdk/client/index.js';
import { StdioClientTransport } from '@modelcontextprotocol/sdk/client/stdio.js';
import { UnityBridge } from '../bridge.mjs';

async function fixture() {
  const project = await realpath(await mkdtemp(path.join(os.tmpdir(), 'audiomw-mcp-')));
  const root = path.join(project, 'Library', 'AudioMWAgent');
  const value = { SchemaVersion: 1, Project: 'a'.repeat(64), ProjectPath: project, Session: randomBytes(16).toString('hex'), Heartbeat: Date.now() };
  const inbox = path.join(root, value.Session, 'requests');
  const outbox = path.join(root, value.Session, 'responses');
  await mkdir(inbox, { recursive: true }); await mkdir(outbox);
  await writeFile(path.join(root, 'connection.json'), JSON.stringify(value));
  return { project, root, value, inbox, outbox, dispose: () => rm(project, { recursive: true, force: true }) };
}
async function service(f, callback) {
  let busy = false;
  const timer = setInterval(async () => {
    if (busy) return; busy = true;
    try {
      for (const name of (await readdir(f.inbox)).filter(n => n.endsWith('.json'))) {
        const request = JSON.parse(await readFile(path.join(f.inbox, name), 'utf8'));
        const result = callback(request);
        await writeFile(path.join(f.outbox, name), JSON.stringify(result));
        await unlink(path.join(f.inbox, name));
      }
    } finally { busy = false; }
  }, 15);
  return async () => { clearInterval(timer); while (busy) await new Promise(r => setTimeout(r, 10)); };
}
test('real stdio SDK negotiation exposes only five review tools and no approval', async () => {
  const f = await fixture(); const stop = await service(f, request => ({ Ok: true, State: 'ready', Inventory: { Project: request.Project, Session: request.Session } }));
  const client = new Client({ name: 'audiomw-test', version: '1' });
  try {
    await client.connect(new StdioClientTransport({ command: process.execPath, args: [path.resolve('server.mjs'), '--project', f.project] }));
    const tools = (await client.listTools()).tools;
    assert.deepEqual(tools.map(t => t.name).sort(), ['audiomw_inspect', 'audiomw_plan_status', 'audiomw_propose', 'audiomw_propose_music', 'audiomw_show_review']);
    const result = await client.callTool({ name: 'audiomw_inspect', arguments: {} });
    assert.equal(result.structuredContent.Inventory.Project, f.value.Project);
    const invalid = await client.callTool({ name: 'audiomw_propose', arguments: { events: [] } }); assert.equal(invalid.isError, true);
  } finally { await client.close(); await stop(); await f.dispose(); }
});
test('requires inspect, checks session restart, refuses an approval command', async () => {
  const f = await fixture(); const b = new UnityBridge(f.project);
  const stop = await service(f, () => ({ Ok: true }));
  try {
    await assert.rejects(b.request('status', {}), /inspect first/); await b.inspect();
    await assert.rejects(b.request('approve', {}), /Unsupported/);
    f.value.Session = randomBytes(16).toString('hex'); await writeFile(path.join(f.root, 'connection.json'), JSON.stringify(f.value));
    await assert.rejects(b.request('status', {}), /session changed/);
  } finally { await stop(); await f.dispose(); }
});
test('refuses wrong project, stale heartbeat and symlink mailbox', async () => {
  const f = await fixture(); const b = new UnityBridge(f.project);
  try {
    f.value.ProjectPath = path.dirname(f.project); await writeFile(path.join(f.root, 'connection.json'), JSON.stringify(f.value)); await assert.rejects(b.inspect(), /identity mismatch/);
    f.value.ProjectPath = f.project; f.value.Heartbeat = 0; await writeFile(path.join(f.root, 'connection.json'), JSON.stringify(f.value)); await assert.rejects(b.inspect(), /heartbeat/);
    const file = path.join(f.root, 'connection.json'); await unlink(file); const other = path.join(f.project, 'other.json'); await writeFile(other, JSON.stringify(f.value)); await symlink(other, file); await assert.rejects(b.inspect(), /symlinks/);
  } finally { await f.dispose(); }
});
test('proposals resolve local files, carry stable IDs and never import assets', async () => {
  const f = await fixture(); const b = new UnityBridge(f.project); let captured;
  const stop = await service(f, request => { captured = request; return { Ok: true, State: request.Operation === 'propose' ? 'pending' : 'ready' }; });
  try {
    await b.inspect(); const file = path.join(f.project, 'click.wav'); await writeFile(file, 'fixture'); const id = 'b'.repeat(32);
    const input = { requestId: id, destination: 'Assets', intent: 'click', events: [{ name: 'Click', purpose: 'UI press', files: [file], replaceEventGuid: 'c'.repeat(32) }] };
    assert.equal((await b.propose(input)).State, 'pending'); assert.equal(captured.Id, id); assert.equal(captured.Draft.Events[0].Files[0], file);
    assert.equal(captured.Operation, 'propose'); assert.equal((await readdir(f.project)).includes('Assets'), false);
    input.events[0].files = ['https://host/audio.wav']; await assert.rejects(b.propose(input), /absolute local/);
    input.events[0].files = [file + 'missing']; await assert.rejects(b.propose(input), /ENOENT/);
  } finally { await stop(); await f.dispose(); }
});
test('timeout reports pending request identity instead of claiming application', async () => {
  const f = await fixture(); const b = new UnityBridge(f.project, 150);
  try { b.bound = await b.connection(); await assert.rejects(b.request('propose', {}, 'c'.repeat(32)), /cccccccccccccccccccccccccccccccc may still be queued/); }
  finally { await f.dispose(); }
});
test('different clients cannot reuse one request ID for different drafts', async () => {
  const f = await fixture(); const a = new UnityBridge(f.project); const b = new UnityBridge(f.project);
  const stop = await service(f, () => ({ Ok: true, State: 'pending' }));
  try {
    await a.inspect(); await b.inspect(); const id = 'd'.repeat(32);
    await a.request('propose', { Draft: { Intent: 'first' } }, id);
    await assert.rejects(b.request('propose', { Draft: { Intent: 'different' } }, id), /different content/);
    assert.equal((await b.request('propose', { Draft: { Intent: 'first' } }, id)).State, 'pending');
  } finally { await stop(); await f.dispose(); }
});

test('music requests preserve explicit stems, channel choices and state transitions without approval', async () => {
  const f = await fixture(); const bridge = new UnityBridge(f.project); let captured;
  const stop = await service(f, request => { captured = request; return { Ok: true, State: 'pending' }; });
  try {
    await bridge.inspect(); const file = path.join(f.project, 'stem.wav'); await writeFile(file, 'fixture');
    const input = { requestId: 'e'.repeat(32), intent: 'Adaptive music', tracks: [{ name: 'Explore', purpose: 'Cafe', tempo: 120, body: file, stems: [{ name: 'Pulse', file, parameterGuid: 'a'.repeat(32), lowWeight: 0, highWeight: 1, forceToMono: true }] }], profile: { name: 'Cafe', states: [{ name: 'Explore', track: 'Explore' }], transitions: [] } };
    assert.equal((await bridge.proposeMusic(input)).State, 'pending');
    assert.equal(captured.Operation, 'propose_music'); assert.equal(captured.MusicDraft.Tracks[0].Stems[0].ForceToMono, true); assert.equal(captured.MusicDraft.Tracks[0].Stems[0].LowWeight, 0); assert.equal(captured.MusicDraft.Profile.Quantization, 'Bar');
    input.tracks[0].body = 'https://host/loop.wav'; await assert.rejects(bridge.proposeMusic(input), /absolute local/);
  } finally { await stop(); await f.dispose(); }
});
