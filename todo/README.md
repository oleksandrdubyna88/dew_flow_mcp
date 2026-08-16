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
| [PLAN_tool_surface_config.md](PLAN_tool_surface_config.md) | plan only, 2026-08-16 | the tool surface as configuration — descriptions resolved from a file catalog with the compiled literal as the floor, a tool subset chosen at process start, a `SurfaceFingerprint` a caller can read back (`--print-surface`, `GET /api/mcp/surface`), and an additive `correlation` on `telemetry/v0` whose reader already expects it. Makes [PLAN_mcp_product.md](PLAN_mcp_product.md)'s *"a tool's description is a measured artefact"* actually runnable: today a wording is a C# literal in the binary, so A/B-ing ten of them is a rebuild each. Deliberately touches nothing inside `ToolCatalog`/`ToolSchema`/`IToolProvider`/`CatalogToolFunction`/`LocalLlmToolBridge` — one decorator ahead of the catalog, so parity holds by construction. Consumer: `dew_flow_benchmark · todo/PLAN_tool_benchmark.md` |
| [PLAN_reliability_tail.md](PLAN_reliability_tail.md) | open; item 2 shipped 2026-08-16, the rest outstanding | what the 24/7 audit found and the same-day fixes did not take: the read cap that is only a telemetry budget (still the one HIGH), the payload budget's per-call cost, explicit HTTP timeouts, and what a never-restarting process does about its log and spool |

Implemented plans live in [`../research/`](../research/) — most recently
[PLAN_usage_telemetry.md](../research/PLAN_usage_telemetry.md) (`ToolResult.Refused`, caller identity
read from the protocol session, byte-budgeted payload capture, and a spool sink emitting the
benchmark-owned `telemetry/v0` schema; 2026-08-15). Its open tail: the product host's registration
line, and the ingest side that lives in `dew_flow_benchmark`.

## This repository is public

Two consequences that shape every plan here:

- It must not know that retrieval exists. `IToolProvider` is declared here and implemented outside; the
  arrow points inward only, and a test in the RAG repo checks it from the other side too.
- Editing tools stay out. The public surface reads; the write path lives in the private product.

## Sibling repositories

- `dew_flow_rag_qln` — the retrieval product that implements these tools (private)
- `dew_flow_sidecar_rust` — the embedding engine underneath (public)
