# PLAN — the reliability tail the 24/7 audit left open

> Status: **plan only, nothing implemented yet, 2026-08-16.** Scope: `src/Mcp.Application`,
> `src/Mcp.Bridge`, `src/Mcp.Host`, `src/Mcp.Telemetry`, `src/ServiceDefaults`. The CRITICAL/HIGH
> defects of the same audit — the overflowing read window, the silently dying spool writer, the
> unguarded dispatch chain, the missing per-call ceiling and the constant health answer — are being
> fixed in a separate task and are **not** in this plan.
>
> Related: `.claude/rules/shared/common/reliability.md` (the doctrine this audit produced).

## Why this document exists

On 2026-08-16, the eve of the first long unattended runs, all four `dew_flow_*` repositories were
audited against one mission: **24/7 operation, no leaks, no hangs, every failure legible in the log
afterwards.** This server is the smallest and cleanest of the four, and its two CRITICAL findings
were both reachable by any connected client in a single call — those are fixed. What remains is
listed here so it is tracked work rather than a paragraph in a chat log that expires.

## The symptom, per item

### 1. The size ceiling that isn't — HIGH, if not already closed by the fix task

`src/Workspace.Infrastructure/SandboxedFileReader.cs:27` reads with `File.ReadAllLinesAsync` — the
whole file, whatever its size — and when a client omits `lineCount` the documented default is "read
to the end". Meanwhile `PayloadBudget.Apply` in `ToolCatalog.RecordAsync` (`ToolCatalog.cs:78-81`)
budgets **only the copy stored in telemetry**; the `ToolResult` returned at line 68 is untruncated.
So the byte budget the comments describe reads as a size ceiling and is not one: nothing bounds what
goes back over stdio or HTTP.

This was handed to the fix task as an optional item, because capping it is a **contract** decision
rather than a patch: a truncated read must say it was truncated and name the real total, or the
caller will page blindly. **Check whether it landed there before starting.** If it did, strike this
item and record it here.

**Fix:** cap lines and bytes in the reader itself; return an explicit truncation marker with the true
line count, so a caller can request the next window by number — the family's established read-window
contract.

### 2. `JsonDocument` is never disposed on the bridge's hot path — MEDIUM

`src/Mcp.Bridge/LocalLlmToolBridge.cs:38-41`:

```csharp
arguments = JsonDocument.Parse(
    string.IsNullOrWhiteSpace(argumentsJson) ? "{}" : argumentsJson).RootElement.Clone();
```

`JsonDocument.Parse` rents from `ArrayPool<byte>.Shared`; undisposed, those buffers are never
returned and the pool's purpose is defeated on every bridge-driven call. The `.Clone()` then pays for
an independent copy that a `using` would make unnecessary. The same shape, once at startup and
therefore harmless, is at `src/Mcp.Contracts/ToolSchema.cs:23-24`.

**Fix:** `using var document = JsonDocument.Parse(...)`, keeping the document alive for the call
instead of cloning out of it. Given as an optional item to the fix task — check first.

### 3. The payload budget pays an O(n log n) cost on every call — MEDIUM

`src/Mcp.Application/PayloadBudget.cs:18-33` runs `Encoding.UTF8.GetByteCount(text)` unconditionally,
and `LongestPrefixWithin:38-62` binary-searches with a fresh `GetByteCount` over a growing prefix each
step. It is called twice per tool call from `ToolCatalog.RecordAsync` — for arguments and for the
response. On small payloads this is noise; composed with item 1 it becomes a real per-call cost on
the hot path, paid to produce a record that will be truncated anyway.

**Fix:** short-circuit when the payload is already under budget by a cheap check (length in chars
bounds bytes from below), and avoid re-counting from zero inside the search.

### 4. No explicit Kestrel or request timeouts on the HTTP transport — MEDIUM

`src/Mcp.Host/Program.cs:36-47` builds the web host with no `KestrelServerOptions` and no
`RequestTimeouts` middleware, relying entirely on framework defaults. The shared rule is explicit
that a framework default you rely on is still a decision, and must be named. This compounds whatever
per-call ceiling the fix task adds inside the catalog: between "the client disconnects" and "the call
finishes" there should be no unnamed gap.

**Fix:** configure keep-alive, request-header and request-body timeouts explicitly, in
`appsettings.json` rather than in code, so an operator can change them without a rebuild.

### 5. Log and spool files grow without bound for a process that never restarts — MEDIUM

`src/ServiceDefaults/McpLogging.cs:76-80` writes one file per run with no `fileSizeLimitBytes` and no
`retainedFileCountLimit`; `src/Mcp.Telemetry/SpoolUsageSink.cs:150-157` fixes one spool path at
construction for the life of the process. Both are correct per the family's "a file per RUN" rule —
but that rule's mitigating rotation *is* the restart, and this deployment's premise is that the
process does not restart. The two facts have never been reconciled.

**Fix:** name the owner, per `.claude/rules/shared/common/logging-serilog.md` § Retention: prune day
folders older than the window at startup, **and** decide what a genuinely months-long process does —
a size-based rollover within the run, or a planned periodic restart recorded in the README. This is
the one item on this list that needs an operator decision rather than a patch.

### 6. `Mcp.Ui` is built and shipped but wired to nothing — LOW

`dew_flow_mcp.slnx:10` includes it, `Mcp.Ui.csproj` builds it, and `Mcp.Host.csproj` does not
reference it. `ArchitectureTests.Every_shipped_project_is_covered_by_the_rule` already treats it as
shipped. It is inert scaffolding — no runtime risk, but a live-looking surface that is not one is a
trap for the next reader, and this repository is public.

**Fix:** either wire it or remove it from the shipped set; if it is deliberate scaffolding for
planned work, say so in its own README so the next reader stops looking for the wiring.

### 7. The console sink flushes under a global lock, per line — LOW

`src/ServiceDefaults/AnsiConsoleSink.cs:52-69` holds a lock across `writer.Flush()` for every event.
Correct for interleaving safety, and invisible at Information volume — but it becomes a contention
and latency point exactly when someone raises verbosity to debug a live problem, which is the moment
it must not.

**Fix:** know it before turning the level up; if it bites, buffer and flush on an interval rather
than per line. Listed so the next person debugging a hot server does not discover it by feel.

## Build order

1. **(1) the read cap** — if the fix task did not close it, it is the only HIGH here.
2. **(2) `JsonDocument`** and **(3) budget cost** — small, same hot path, one pass.
3. **(4) explicit HTTP timeouts** — configuration, no behaviour change expected.
4. **(5) retention** — needs the operator decision; start the conversation early.
5. **(6) `Mcp.Ui`** and **(7) the sink note** — hygiene, any time.

## Test plan

Per `.claude/rules/shared/common/testing.md`, each behavioural item starts with a RED test named for
the guarantee, observed failing for the real symptom:

| item | test name |
|---|---|
| 1 | `A_read_larger_than_the_cap_says_it_was_truncated_and_names_the_real_total` |
| 2 | `A_bridge_call_returns_its_pooled_buffers` |
| 3 | `A_payload_already_within_budget_is_not_re_measured` |
| 5 | `A_day_folder_older_than_the_window_is_pruned_at_startup` |

Items 4, 6 and 7 have no observable behaviour to assert on their own — say so explicitly in the
summary when skipping, per the rule's Scope section.

Build, then run the test project's executable under `tests/.../bin/Debug/net10.0/` — never
`dotnet test`. If Smart App Control kills a freshly built runner with `0x800711C7`, retry the same
binaries ~60 s apart without rebuilding.

## Definition of Done

- [ ] Item 1 is confirmed either closed by the fix task or implemented here — not assumed.
- [ ] Every other item is implemented, or explicitly declined here with the reason recorded.
- [ ] Each implemented behavioural item has a RED-then-GREEN test, both observations quoted.
- [ ] Timeouts and retention windows live in `appsettings.json`, not in code.
- [ ] `logs/` and the spool have a named retention owner, and the never-restarting case is answered.
- [ ] Nothing writes to stdout in the stdio host — the protocol contract still holds after the change.
- [ ] On completion the plan is promoted to `research/` with its deviations recorded, and the
      *Currently open* table in [README.md](README.md) is updated in the same task.
