# todo/

Plans for work that is **not finished**. Documentation of the system as it *is* belongs in `research/`.

The test is one question: **is someone still supposed to build this?** If yes it lives here; once it ships,
the plan moves to `research/` with its status changed to `IMPLEMENTED <date>` and its deviations recorded.

Every plan starts with a status line on line 2–3 and carries: the symptom or goal before any solution,
references to real code as `file.cs:line` (verified, not guessed), a build order, a test plan, and a
Definition of Done.

## Currently open

| plan | status | scope |
|---|---|---|
| [PLAN_mcp_product.md](PLAN_mcp_product.md) | inversion and both transports built and parity-tested; the tool set is one placeholder | the three tool families, auth, public-repository hygiene (usage metering now shipped — see below) |
| [PLAN_reliability_tail.md](PLAN_reliability_tail.md) | open; **every item shipped 2026-08-16**, held open only by the shared-rule reconciliation | what the 24/7 audit found and the same-day fixes did not take. Shipped: the read cap (streaming, two caps, a legible truncation marker), the undisposed `JsonDocument`, the payload budget's per-call cost (a cheap upper-bound accept plus a single forward pass), explicit Kestrel and request timeouts in `appsettings.json` — with **no** global request-timeout policy, because it would have severed the SSE stream — and the console sink's per-line lock (rendered outside it, one write inside). Item 5 was answered by the operator with a third option neither candidate named: a file per run, **segmented at UTC midnight**, in both the log and the spool. Item 6 was wired rather than removed: `Mcp.Ui` gained a real `/mcp` page — the human-readable face of the `SurfaceFingerprint` — plus a read side where an unreachable daemon and a server with no tools render as two different messages. Its consuming half — the submodule pin, an `McpModule`, the router slice, the nav entry and the AppHost URL — landed in `dew_flow_rag_qln`. Retention followed on a second instruction: `LogRetention.Prune` deletes log day folders older than `Mcp:Logs:RetentionDays` (30) at startup, while the SPOOL is deliberately left to its ingester — only the consumer knows which records it has taken, so deleting on its behalf would destroy telemetry nobody read. One thing genuinely unfinished: the midnight segment makes this repository's logging differ from the shared rule in `dew_flow_conventions` and from the two sibling repos |

## Promoted

Implemented plans live in [`../research/`](../research/), newest first.

| plan | landed | what it delivered |
|---|---|---|
| [PLAN_tool_surface_config.md](../research/PLAN_tool_surface_config.md) | 2026-08-16 | The tool surface as configuration: descriptions read from `<dir>/<set>/<tool>.md` with the compiled literal as the floor, a tool subset chosen at process start, guards that stop the host on a configuration that does not fit, a `SurfaceFingerprint` readable via `--print-surface` and `GET /api/mcp/surface`, and an additive `correlation` on `telemetry/v0` stamped from `--correlation` and refused on the shared HTTP transport. `ToolCatalog`, `ToolSchema`, `IToolProvider`, `CatalogToolFunction` and `LocalLlmToolBridge` are untouched — one decorator ahead of the catalog, so parity holds by construction. Open tail: its own §7 questions (a set-able argument schema; a version stamp inside a set), and one finding for `dew_flow_benchmark` — its spool fixture can no longer be both today's emitter output and a pre-`correlation` line, so that repository needs a second fixture rather than a replaced one |
| [PLAN_usage_telemetry.md](../research/PLAN_usage_telemetry.md) | 2026-08-15 | `ToolResult.Refused`, caller identity read from the protocol session, byte-budgeted payload capture, and a spool sink emitting the benchmark-owned `telemetry/v0` schema. Open tail: the product host's registration line, and the ingest side that lives in `dew_flow_benchmark` |

## This repository is public

Two consequences that shape every plan here:

- It must not know that retrieval exists. `IToolProvider` is declared here and implemented outside; the
  arrow points inward only, and a test in the RAG repo checks it from the other side too.
- Editing tools stay out. The public surface reads; the write path lives in the private product.

## Sibling repositories

- `dew_flow_rag_qln` — the retrieval product that implements these tools (private)
- `dew_flow_sidecar_rust` — the embedding engine underneath (public)
