# PLAN — usage telemetry that leaves the building: caller, outcome, budgeted payloads, spool

> Status: **IMPLEMENTED in this repository, 2026-08-15** — authored and shipped the same day; 54 tests
> green, 0 warnings. Scope: `src/Mcp.Contracts` (the `ToolUsage`/`ToolResult` contracts),
> `src/Mcp.Application` (capture at the dispatch point), a new `src/Mcp.Telemetry` (the spool sink),
> `src/Mcp.Host` registration.
>
> **Deviations, and one measured defect.**
> 1. **`BoundedChannelFullMode.DropWrite` was the wrong choice and a test caught it.** It reports
>    success while discarding the record, so the drop counter stayed at zero: 500 records recorded at
>    capacity 1, **2 written, 0 counted**. Changed to `Wait` paired with `TryWrite` — which does not
>    wait, it refuses — so every record is now either written or counted. This is exactly the silent
>    drop the sink exists to make impossible, and it was in the sink itself.
> 2. **Unknown-tool calls are now metered**, reversing the previous behaviour (and an existing test's
>    assertion). A client hammering a tool this server does not advertise — a stale configuration,
>    almost always — was the one event nobody could see.
> 3. **The model override moved from configuration to the bridge.** `Mcp:Telemetry:CallerModel` was
>    planned; the honest home is `AddLocalLlmToolBridge(model)`, because the in-process bridge is the
>    only presentation whose host actually knows which model it drives.
> 4. **`ToolResult.Text`** was added — the one projection every consumer wanted, rather than three
>    hand-written matches.
> 5. **The caller-identity capture is verified through a real protocol session**
>    (`ProtocolCallerIdentityTests`: a real `McpClient` against a real `McpServer` over an in-memory
>    stream pair), not through a hand-made context — the claim is about what the transport carries.
>
> **Open tail (not done here).** The Daemon registration line in the private product repo
> (`dew_flow_rag_qln · hosts/Daemon/Program.cs`) is NOT yet added, so real product traffic is still
> unmetered; and the ingest half lives in the benchmark, which has not been built yet — no spool has
> been drained end to end. Both are tracked by the benchmark's plan.
>
> This is the emitter half of a cross-repository contract. The schema (`telemetry/v0`), the ingest
> and the report live in the benchmark: `dew_flow_benchmark · todo/PLAN_tool_telemetry_v0.md`. The
> owner of the contract is the benchmark repository (operator decision, 2026-08-15); this plan
> implements Phase 2's "usage metering" bullet of [PLAN_mcp_product.md](PLAN_mcp_product.md) against
> that schema.

## Symptom

Every tool call already reaches one choke point — `ToolCatalog.InvokeAsync`
([ToolCatalog.cs:35](../src/Mcp.Application/ToolCatalog.cs)) — which already measures duration,
failure and response size and forwards them to `IUsageSink`
([IUsageSink.cs:13](../src/Mcp.Contracts/IUsageSink.cs)). But:

- the only sink that exists is `NullUsageSink` — the data is discarded, in the product host too;
- `ToolUsage` is a bare counter (name, duration, failed, chars) — no caller, no arguments, no
  outcome triage, no scope;
- **who is calling is never read**: the MCP `initialize` handshake carries the client's name and
  version, and nothing in the pipeline looks at it — `CatalogToolFunction.InvokeCoreAsync`
  ([CatalogToolFunction.cs:22](../src/Mcp.Server/CatalogToolFunction.cs)) sees only arguments;
- a **refusal is indistinguishable from an error**: `ToolResult` is `Ok | Failed`
  ([ToolCall.cs:13](../src/Mcp.Contracts/ToolCall.cs)), so a sandbox denial and an IO failure record
  identically — the exact two-states-for-three-facts defect the benchmark's lessons file pins
  (a read-only guarantee was once asserted for months on that basis, and was false).

## What one call records (the `telemetry/v0` shape, owned by the benchmark)

Per call: tool name · UTC timestamp · caller (client name/version from `initialize`, transport,
model — each **captured-or-not**, never guessed; the MCP protocol does not carry the model, so for
real sessions it is *not captured* and benchmark legs self-declare theirs) · scope (workspace root /
project) · arguments JSON within a byte budget with truncation recorded · outcome
(**answered | refused | error**) · error text · response size always + body within its own budget ·
tokens (captured-or-not; this surface never knows them in v0) · server-side duration.

Budgets are applied **at emit** (default 4 KB arguments, 4 KB response body) — the spool never holds
more than the budget, which is the retention decision made before the first write.

## Design

1. **`ToolResult` grows its third case: `Refused(string Reason)`.** The union's own doc says adding
   a case must force every consumer to notice — `Match` gains a third function and the compiler finds
   every call site. Both presentations map `Refused` to the wire exactly as `Failed`
   (`isError: true` — the protocol has no third state and `ProtocolErrorFlagTests` stays the guard);
   only telemetry and callers of `Match` see the difference. `SandboxedFileReader`'s path-escape and
   out-of-root denials become `Refused`; genuine IO/parse failures stay `Failed`.
2. **`ToolUsage` stops being a counter** — deliberate scope change to the type whose comment says
   "it must stay one"; the comment goes with it. It becomes the v0 record (minus the envelope the
   sink adds): tool, at, caller, scope, budgeted arguments + truncation, outcome, error, response
   chars + budgeted body + truncation, tokens, duration. `NullUsageSink` stays the default.
3. **Caller identity is an ambient, transport-filled context.** New `ICallerContext` in
   `Mcp.Contracts` (`CallerIdentity Current { get; }` with captured-or-not fields); an `AsyncLocal`
   implementation in `Mcp.Application`. The MCP presentation fills it from the session's
   `IMcpServer.ClientInfo` (SDK exposes the `initialize` client info per session; the exact seam —
   DI on the per-session provider vs a hook at `MapMcp` — is settled at implementation against
   `ModelContextProtocol` 2.2.0); the bridge fills it with the local model's name (the bridge host
   *does* know its model); unfilled fields are *not captured* with the reason. A benchmark caller
   can override via configuration (`Mcp:Telemetry:CallerModel`) — that is how bench legs are fully
   attributed.
4. **`SpoolUsageSink` in a new `src/Mcp.Telemetry`** (references `Mcp.Contracts` only): serializes
   `telemetry/v0` JSONL to `{spoolRoot}/{yyyy-MM-dd}/{app}-{HH-mm-ss}-{pid}.jsonl` — UTC, one file
   per run, mirroring the logging rule's path shape. **It may never block or fail a tool call**: a
   bounded channel feeds a background writer; overflow drops the record and counts the drop; a
   failing disk trips a breaker with one log line, not one per call. Flush on host shutdown.
5. **Registration**: `AddMcpApplication` keeps `NullUsageSink` as the `TryAdd` default
   ([McpApplicationExtensions.cs:14](../src/Mcp.Application/McpApplicationExtensions.cs)); each host
   overrides when `Mcp:Telemetry:SpoolDir` is configured — `Mcp.Host` here, and one line in the
   product's `Daemon` composition root so real traffic is collected too.

## Build order

1. Contracts: `ToolResult.Refused`, three-arm `Match`, `CallerIdentity`/`ICallerContext`, the new
   `ToolUsage`. Compiler-led sweep of every `Match` call site.
2. `ToolCatalog.RecordAsync` builds the full record (arguments/body budgeting as pure static
   helpers); `WorkspaceToolProvider`/`SandboxedFileReader` refusals become `Refused`.
3. `Mcp.Telemetry`: the spool sink + options; tests.
4. Presentation fill: MCP server session → `ICallerContext`; bridge → local model identity.
5. Host registration (`Mcp.Host`, then the Daemon line in the product repo).
6. `todo/README.md` table + `PLAN_mcp_product.md` Phase 2 bullet annotated as in-flight here.

## Test plan

- `ToolCatalogTests`: every outcome — answered, refused, error, unknown-tool — reaches the sink as
  its own state; duration and sizes populated; the existing every-call-reaches-the-sink guarantee
  holds.
- Budgeting: an oversized argument object is cut at the budget with `TruncatedBytes` exact; an exact
  -budget payload is not marked truncated.
- `Refused` on the wire: both presentations still emit `isError` (extend `ProtocolErrorFlagTests`);
  the bridge's JSON shape unchanged.
- Spool sink: writes valid single-line JSON per record (round-trip against the v0 codec fixture the
  benchmark repo commits); a full channel drops rather than blocks (fault injection with a stalled
  writer); a sink that throws never fails the dispatched call.
- Caller context: unfilled → *not captured* with reason; filled by the MCP presentation when
  `ClientInfo` exists; config override wins for the model field.
- Architecture guard: `Mcp.Telemetry` references contracts only; `SurfaceParityTests` untouched.

## Definition of Done

- [x] Outcome is three-state end to end; a sandbox denial records as `refused`, never as `error`.
- [x] Every call carries caller identity to the limit the transport knows; model is *not captured*
      for real sessions and self-declared for benchmark legs — never guessed.
- [x] Arguments and response bodies are byte-budgeted at emit; truncation is recorded exactly.
- [x] The spool sink never blocks, never fails a call, and flushes on shutdown.
- [ ] **A spool file produced here ingests cleanly by `bench telemetry ingest`** — the ingest side
      does not exist yet, so this is the one item the emitter cannot close alone.
- [x] `NullUsageSink` remains the default; hosts opt in by configuration (`--spool <path>`).
- [x] Both `todo/README.md` tables and the product plan's Phase 2 bullet reflect this plan.
- [ ] **The product host registers the sink** (`dew_flow_rag_qln · hosts/Daemon/Program.cs`), so real
      traffic is recorded and not only the standalone host's.
