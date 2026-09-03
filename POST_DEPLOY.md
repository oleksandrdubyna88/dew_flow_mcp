# Post-deploy checks — dew_flow_mcp

Per [`.claude/rules/shared/common/post-deploy-checks.md`](.claude/rules/shared/common/post-deploy-checks.md).

This server is not deployed to anything: it is **installed and run** — as an HTTP/SSE endpoint a
runtime connects to, or as a subprocess a runtime spawns. So "prod" here is the process an operator
actually has running, and the list is run at every release against it. That is the rule's second row,
and it is the one people skip.

Target: the running management surface, as an origin — `--target http://127.0.0.1:5211`
Last verified: 2026-09-03 · http://127.0.0.1:5211 · 1.0.0+ebd6e7e — all four automated items PASS, and item 2 was watched FAIL against a wrong `EXPECTED_COMMIT`, so the check is known to have teeth. Item 5 is a person's.

| # | What a person loses if this is broken | Check | Auto |
|---|---|---|---|
| 1 | Every tool this server offers is silently gone: an agent falls back to grep, answers get worse, and **nothing says why** — which is exactly how a connection refusal presents | `node -e "fetch(process.env.TARGET+'/api/mcp/health').then(r=>r.json()).then(h=>process.exitCode=+(h.status==='ok'?0:1))"` | auto |
| 2 | The process is an older build than the one you installed. Agents keep calling tools the way the old descriptions taught them, the calls stay valid, and the answers are quietly worse — `VERSIONING.md` exists for this | `node -e "fetch(process.env.TARGET+'/api/mcp/surface').then(r=>r.json()).then(s=>process.exitCode=+(String(s.version.value).includes(process.env.EXPECTED_COMMIT\|\|'\\u0000')?0:1))"` | auto |
| 3 | The tool surface changed shape without anyone deciding to change it — a renamed tool or a rewritten description is a MAJOR change to a caller that cannot read a changelog | `node -e "fetch(process.env.TARGET+'/api/mcp/surface').then(r=>r.json()).then(s=>process.exitCode=+(s.toolsHash===process.env.EXPECTED_TOOLS_HASH?0:1))"` | auto |
| 4 | A dead telemetry writer behind a healthy-looking probe: `status` is `ok` when nothing was checked, so the components list is the only thing that distinguishes "all fine" from "nothing asked" | `node -e "fetch(process.env.TARGET+'/api/mcp/surface').then(r=>r.json()).then(s=>{console.log(s.app,s.pid,s.tools.length+' tools');process.exitCode=+(s.tools.length>0?0:1)})"` | auto |
| 5 | The server is serving the wrong workspace — every answer is about a tree nobody asked about, and every one of them looks plausible | The `--root` it was started with is not on the wire. Read the process's own command line and confirm it names the workspace you meant | manual |

## The two variables items 2 and 3 need

They are the point of those items rather than an inconvenience: a check that cannot say **which**
build you meant can only confirm that *a* server is running.

```bash
# from the build you installed, before starting it
dotnet run --project src/Mcp.Host -- --print-surface
export EXPECTED_COMMIT=$(git rev-parse --short HEAD)
export EXPECTED_TOOLS_HASH=<toolsHash from the line above>

node .claude/rules/shared/tools/post-deploy-check.mjs --target http://127.0.0.1:5211
```

Without them item 2 compares against a value that cannot occur and item 3 against `undefined`, so
both fail — deliberately, and loudly, rather than passing on no evidence.
