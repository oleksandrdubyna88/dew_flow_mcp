# Claude Code — Project Rules for dew_flow_mcp

These rules apply to all code in this repository and override Claude's defaults. The family-wide
doctrine lives in [.claude/rules/shared](.claude/rules/shared) (a submodule of
`dew_flow_conventions`) — this file carries only what is specific to THIS repository, deliberately:
a copied rule is a mirror that drifts.

## Project Overview

`dew_flow_mcp` is the family's **public MCP tool surface**: a retrieval-agnostic tool
catalog/dispatch framework with two presentations (a stdio/HTTP protocol server and an in-process
LLM bridge), per-call telemetry spooling, and exactly one real tool — `rt_read_local_file`,
sandboxed to `--root`. It knows nothing about retrieval; `dew_flow_rag_qln` consumes it as the
`external/dew_flow_mcp` submodule and plugs its own providers into the catalog.

**This repository is public.** LICENSE/NOTICE/THIRD-PARTY-NOTICES are load-bearing;
[SECURITY.md](SECURITY.md) states the threat model and must keep matching the code — it claimed
symlink-escape rejection before the guard could actually see links (fixed 2026-08-19), and that gap
is the cautionary example. **Read first:** [README.md](README.md), then
[research/architecture.md](research/architecture.md) and the relevant `research/module_*.md`.

## Commands

```bash
# Build
dotnet build dew_flow_mcp.slnx -c Debug

# Run tests — ALWAYS via the test project's executable, NEVER `dotnet test`
# (xUnit v3 / Microsoft Testing Platform: there is no VSTest testhost, so `dotnet test` aborts)
./tests/Mcp.Tests/bin/Debug/net10.0/Mcp.Tests.exe
./tests/Mcp.Tests/bin/Debug/net10.0/Mcp.Tests.exe --filter-class "*WorkspaceToolTests"

# Run the server
dotnet run --project src/Mcp.Host                        # HTTP/SSE, the default
dotnet run --project src/Mcp.Host -- --stdio             # stdio, for a runtime that spawns it
dotnet run --project src/Mcp.Host -- --print-surface     # what this build advertises, as JSON
# --root <path>  the workspace the tools may touch (default: current directory)
# --spool <path> per-call telemetry; absent, nothing is written
```

## Project Structure

| Project | Role |
|---------|------|
| `src/Mcp.Contracts` | Tool/result/captured shapes; depends on nothing |
| `src/Mcp.Application` | `ToolCatalog`, `IToolProvider` — the dispatch core |
| `src/Mcp.Server` | The MCP protocol presentation (stdio + HTTP/SSE) |
| `src/Mcp.Bridge` | The in-process LLM bridge presentation |
| `src/Mcp.Host` | The standalone host wiring both |
| `src/Mcp.Api` / `src/Mcp.Ui` | Console API + UI |
| `src/Mcp.Telemetry` | The `telemetry/v0` spool emitter |
| `src/Workspace.*` | The `rt_` tools: the real filesystem, sandboxed to `--root` |
| `tests/Mcp.Tests` | xUnit v3 on Microsoft Testing Platform |

## Repository-specific rules

1. **The wire contract is pinned on the emitter.** `telemetry/v0` is owned by `dew_flow_benchmark`;
   every leaf of the wire tree is asserted by name in `TelemetryWireShapeTests` — editing that list
   IS editing the published schema, and the consumer's codec and fixture move in the same change
   ([research/telemetry_v0_wire.md](research/telemetry_v0_wire.md)).
2. **The sandbox is the security boundary.** Any change to `SandboxedFileReader`'s guard updates
   [SECURITY.md](SECURITY.md) and the escape tests in the same task — the claim and the code have
   drifted apart once already.
3. **No editing tools.** The public surface reads; that boundary is a product decision, a security
   property, and an asserted test. Do not add a write tool casually.
4. **Read-only surface, shared transport honesty.** A correlation declaration on the shared HTTP
   transport is refused, not quietly applied — see `TelemetryCorrelation.Declared`.

## Definition of Done

- [ ] `dotnet build dew_flow_mcp.slnx` — 0 warnings (warnings are errors here).
- [ ] The test executable runs green; new behaviour has tests; a fix has a test watched failing first.
- [ ] A wire or sandbox change carried its documentation (telemetry_v0_wire.md / SECURITY.md) with it.
- [ ] Any plan the work finished was promoted with its deviations recorded
      (`node .claude/rules/shared/tools/plan-lifecycle.mjs` is CI's check).
