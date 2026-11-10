import { readFile } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import { Client } from '@modelcontextprotocol/sdk/client/index.js';
import { StdioClientTransport } from '@modelcontextprotocol/sdk/client/stdio.js';

const [project, operation = 'inspect', input] = process.argv.slice(2);
const client = new Client({ name: 'audiomw-json-client', version: '0.1.0' });
try {
  await client.connect(new StdioClientTransport({ command: process.execPath, args: [fileURLToPath(new URL('./server.mjs', import.meta.url)), '--project', project] }));
  const invoke = async (name, args) => {
    const reply = await client.callTool({ name, arguments: args });
    if (reply.isError) throw new Error(reply.content.map(c => c.text || '').join('\n'));
    return reply.structuredContent;
  };
  const identity = await invoke('audiomw_inspect', {});
  let result = identity;
  if (operation === 'propose') result = await invoke('audiomw_propose', JSON.parse(await readFile(input, 'utf8')));
  else if (operation === 'status') result = await invoke('audiomw_plan_status', { planId: input });
  else if (operation === 'review') result = await invoke('audiomw_show_review', { planId: input });
  else if (operation !== 'inspect') throw new Error('Use inspect, propose <json-file>, status <plan-id>, or review <plan-id>.');
  process.stdout.write(JSON.stringify(result, null, 2) + '\n');
} catch (error) { process.stderr.write(error.message + '\n'); process.exitCode = 1; }
finally { await client.close(); }
