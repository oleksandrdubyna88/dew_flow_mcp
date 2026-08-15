# research/

Documentation of the system **as it is** — plus the design records of plans that already shipped.

The test is one question: **does this describe code that exists today?** If yes it lives here. Work
still to be built lives in [`../todo/`](../todo/), and a plan moves across when it ships, with its
status changed to `IMPLEMENTED <date>` and its **deviations recorded** — what shipped differently is the
most valuable part of the record.

## What is here

| Document | What it is |
|---|---|
| [architecture.md](architecture.md) | The whole system: what it is, the projects, the container diagram, one call end to end, the cross-cutting rules, and an explicit list of what does **not** exist yet |
| [module_tool_dispatch.md](module_tool_dispatch.md) | `Mcp.Contracts` + `Mcp.Application` — the contract and the single dispatch point |
| [module_presentations.md](module_presentations.md) | `Mcp.Server` + `Mcp.Bridge` — two surfaces over one catalog, and where caller identity comes from |
| [module_workspace_tools.md](module_workspace_tools.md) | `Workspace.*` — the one real tool and its sandbox |
| [module_telemetry.md](module_telemetry.md) | `Mcp.Telemetry` — per-call recording, the spool, and why it can never fail a call |
| [module_hosting.md](module_hosting.md) | `Mcp.Host` + `Mcp.Api` + `ServiceDefaults` — composition and logging |
| [PLAN_usage_telemetry.md](PLAN_usage_telemetry.md) | Design record, IMPLEMENTED 2026-08-15 — the third `ToolResult` case, caller identity, byte-budgeted capture, the spool sink |
| [telemetry_v0_wire.md](telemetry_v0_wire.md) | The `telemetry/v0` line this server emits, as implemented — the emitter half of a contract owned by `dew_flow_benchmark` |

## Cross-repository citations

This repository is one of four. Citations to the others are written as **paths, not links** —
`dew_flow_benchmark · todo/PLAN_tool_telemetry_v0.md` — because a relative link that only resolves on
one machine is worse than a citation that names its source.
