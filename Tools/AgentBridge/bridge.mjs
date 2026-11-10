import { readFile, writeFile, rename, mkdir, realpath, stat, lstat, unlink, link } from 'node:fs/promises';
import path from 'node:path';
import { randomBytes } from 'node:crypto';

const delay = ms => new Promise(resolve => setTimeout(resolve, ms));
const idPattern = /^[a-f0-9]{32}$/;

export class UnityBridge {
  constructor(project, timeoutMs = 15000) {
    if (!project || !path.isAbsolute(project)) throw new Error('--project must be an absolute Unity project path.');
    this.project = project;
    this.timeoutMs = timeoutMs;
  }
  async connection() {
    const project = await realpath(this.project);
    const root = path.join(project, 'Library', 'AudioMWAgent');
    const descriptor = path.join(root, 'connection.json');
    await this.noLinks(descriptor);
    let value;
    try { value = JSON.parse(await readFile(descriptor, 'utf8')); }
    catch { throw new Error('Open this Unity project and enable Window > AudioMW > Agent Review.'); }
    if (value.SchemaVersion !== 1 || !idPattern.test(value.Session) || !/^[a-f0-9]{64}$/.test(value.Project) || await realpath(value.ProjectPath) !== project) throw new Error('Unity project or bridge identity mismatch.');
    if (!Number.isFinite(value.Heartbeat) || Math.abs(Date.now() - value.Heartbeat) > 10000) throw new Error('Unity bridge heartbeat is stale. Keep the Editor responsive in Edit Mode.');
    return { ...value, root };
  }
  async noLinks(file) {
    for (let part = file; part !== path.dirname(part); part = path.dirname(part)) {
      try { if ((await lstat(part)).isSymbolicLink()) throw new Error('Bridge paths must not contain symlinks.'); }
      catch (e) { if (e.code !== 'ENOENT') throw e; }
    }
  }
  async inspect() {
    this.bound = await this.connection();
    return this.request('inspect', {});
  }
  async request(operation, payload, requestId = randomBytes(16).toString('hex')) {
    if (!['inspect', 'propose', 'status', 'review'].includes(operation)) throw new Error('Unsupported operation. Approval is only available in Unity.');
    if (!this.bound) throw new Error('Call audiomw_inspect first to bind the intended Unity project/session.');
    if (!idPattern.test(requestId)) throw new Error('requestId must contain 32 lowercase hex characters.');
    const current = await this.connection();
    if (current.Project !== this.bound.Project || current.Session !== this.bound.Session) throw new Error('Editor session changed. Inspect again and create a fresh proposal.');
    const request = { SchemaVersion: 1, Id: requestId, Project: current.Project, Session: current.Session, Operation: operation, ...payload };
    const base = path.join(current.root, current.Session);
    const output = path.join(base, 'responses', requestId + '.json');
    const input = path.join(base, 'requests', requestId + '.json');
    await this.noLinks(input); await this.noLinks(output);
    const temporary = input + '.' + randomBytes(8).toString('hex') + '.tmp';
    const json = JSON.stringify(request);
    if (Buffer.byteLength(json) > 128 * 1024) throw new Error('Request exceeds 128 KB.');
    const claims = path.join(base, 'claims');
    await this.noLinks(claims); await mkdir(claims, { recursive: true });
    const claim = path.join(claims, requestId + '.json'); await this.noLinks(claim);
    await writeFile(temporary, json, { flag: 'wx', mode: 0o600 });
    try { await link(temporary, claim); }
    catch (error) {
      if (error.code !== 'EEXIST') { await unlink(temporary); throw error; }
      if (await readFile(claim, 'utf8') !== json) { await unlink(temporary); throw new Error('requestId already belongs to different content. Use a new ID.'); }
    }
    await rename(temporary, input);
    const started = Date.now();
    while (Date.now() - started < this.timeoutMs) {
      await delay(100);
      try {
        await stat(input);
        continue;
      } catch (e) { if (e.code !== 'ENOENT') throw e; }
      try {
        await this.noLinks(output);
        const info = await stat(output);
        if (info.size > 2 * 1024 * 1024) throw new Error('Unity reply exceeds 2 MB.');
        const reply = JSON.parse(await readFile(output, 'utf8'));
        if (!reply.Ok) throw new Error(reply.Error || 'Unity rejected the request.');
        return reply;
      } catch (e) { if (e.code !== 'ENOENT') throw e; }
    }
    throw new Error(`Unity timed out. Request ${requestId} may still be queued. Retry with the same requestId for proposals; do not assume assets changed.`);
  }
  async propose(input) {
    const events = [];
    for (const event of input.events) {
      const files = [];
      for (const file of event.files) {
        if (!path.isAbsolute(file)) throw new Error('Supply absolute local attachment paths, not URLs or chat attachment IDs.');
        files.push(await realpath(file));
      }
      events.push({ Name: event.name, Purpose: event.purpose, Files: files, ReplaceEventGuid: event.replaceEventGuid || '', MixerGroupId: event.mixerGroupId || '' });
    }
    return this.request('propose', { Draft: { Destination: input.destination, Intent: input.intent, Events: events } }, input.requestId);
  }
}
