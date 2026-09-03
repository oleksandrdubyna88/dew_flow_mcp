# `http/` — the management surface's contract suite

One folder per route group, per
[`.claude/rules/shared/common/http-contracts.md`](../.claude/rules/shared/common/http-contracts.md).

| Folder | Routes |
|---|---|
| [`mcp/`](mcp) | `GET /api/mcp/health`, `GET /api/mcp/surface` — the whole HTTP surface |

**What is deliberately not here.** Everything else this server does is the MCP protocol itself, over
SSE or stdio. Neither is a request you can write down as `.http`, and the rule says so: a tool surface
has a schema and a fingerprint (`--print-surface`), and `VERSIONING.md` decides what a breaking change
to it is. Inventing `.http` files for it would produce a folder of nothing.

## Running it

```bash
npm ci --prefix http                                    # once per machine

ASPNETCORE_URLS=http://127.0.0.1:5211 dotnet run --project src/Mcp.Host &
node .claude/rules/shared/tools/http-run.mjs --env local --target http://127.0.0.1:5211
```

The host needs no configuration for this: the management surface is anonymous, carries no secret and
answers questions about the running process. There is no environment contract beyond "the server is
up", which is why this suite has no tokens where the vault's has four.

The verdict is the exit code — `0` pass · `1` contract regression · `3` environment · `4`
configuration · `5` no valid report.

## A note on what `health` answers

A server started without `--spool` registers no health contributor, so the honest answer is `ok` with
an **empty** components list — "everything that was checked is fine", not "everything is fine". The two
are different sentences and only the components field separates them. The suite asserts the shape that
keeps them separable; it does not assert that anything was checked, because on this server that is a
process argument rather than a property of the code.
