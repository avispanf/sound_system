# AudioMW MCP companion

Install with `npm ci --ignore-scripts`, then configure `node server.mjs --project /absolute/UnityProject` as a stdio MCP server. Requires Node 20+. The Unity package must contain AudioAgentBridge and its Agent Review window; enable it for the current editor session.

Read `agent-mcp.md` for setup, chat workflow, schemas, preservation, trust boundaries and limitations. It is in `Documentation` at the repository or prepared project root, and in `Documentation~` inside the UPM package. Run `npm test` for protocol/transport tests. `client.mjs` is a deterministic MCP SDK client with inspect/propose/review/status commands and deliberately no approval command.

This directory is the companion, not an npm-published package. Do not invent an `npx audiomw` install command. The Unity UPM package has no Node dependency at runtime.
