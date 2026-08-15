# research/

Documentation of the system **as it is** — plus the design records of plans that already shipped.

The test is one question: **does this describe code that exists today?** If yes it lives here. Work
still to be built lives in [`../todo/`](../todo/), and a plan moves across when it ships, with its
status changed to `IMPLEMENTED <date>` and its **deviations recorded** — what shipped differently is
the most valuable part of the record.

## What is here

| Document | What it is |
|---|---|
| [PLAN_usage_telemetry.md](PLAN_usage_telemetry.md) | Design record, IMPLEMENTED 2026-08-15 — per-call telemetry: the third `ToolResult` case, caller identity read from the protocol session, byte-budgeted payload capture, and the spool sink |
| [telemetry_v0_wire.md](telemetry_v0_wire.md) | The `telemetry/v0` line this server emits, as implemented — the emitter half of a contract whose schema is owned by `dew_flow_benchmark` |

## Cross-repository citations

This repository is one of four. Citations to the others are written as **paths, not links** —
`dew_flow_benchmark · todo/PLAN_tool_telemetry_v0.md` — because a relative link that only resolves on
one machine is worse than a citation that names its source.
