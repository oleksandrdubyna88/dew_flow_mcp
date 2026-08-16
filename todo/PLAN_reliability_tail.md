# PLAN — the reliability tail the 24/7 audit left open

> Status: **open; items 1 and 2 shipped 2026-08-16, items 3–7 outstanding.** Scope:
> `src/Mcp.Application`, `src/Mcp.Bridge`, `src/Mcp.Host`, `src/Mcp.Telemetry`, `src/ServiceDefaults`.
> The CRITICAL/HIGH defects of the same audit — the overflowing read window, the silently dying spool
> writer, the unguarded dispatch chain, the missing per-call ceiling and the constant health answer —
> were fixed in a separate task on 2026-08-16 and are **not** in this plan.
>
> Related: `.claude/rules/shared/common/reliability.md` (the doctrine this audit produced).

## Why this document exists

On 2026-08-16, the eve of the first long unattended runs, all four `dew_flow_*` repositories were
audited against one mission: **24/7 operation, no leaks, no hangs, every failure legible in the log
afterwards.** This server is the smallest and cleanest of the four, and its two CRITICAL findings
were both reachable by any connected client in a single call — those are fixed. What remains is
listed here so it is tracked work rather than a paragraph in a chat log that expires.

## The symptom, per item

### 1. The size ceiling that isn't — HIGH, **DONE 2026-08-16**

Closed by the streaming rewrite this item asked for. What shipped:

- **The read streams.** `SandboxedFileReader.WindowAsync` enumerates `File.ReadLinesAsync` (cancellation
  token passed through), keeps only the window, and deliberately runs on **to the end of the file** to
  count the true total — the caller pages by that number, and the end of the file is the only place it
  can be counted. The pass costs I/O and no memory, it is no more I/O than the old read already did, and
  `ToolCatalogOptions.CallTimeout` from the previous task is what bounds how long it may take.
- **Two caps, not one.** `SandboxedFileReaderOptions` — `MaxLines` (5 000) and `MaxBytes` (1 MiB),
  `TryAdd`-registered in `AddWorkspaceTools`, guarded at construction — because a line cap alone does not
  bound a minified bundle (one line, however many megabytes) and a byte cap alone lets five thousand
  short lines out as one block.
- **The truncation is legible.** `FileReadOutcome.Ok` carries a `ReadTruncation` (the family's
  flag-and-reason shape), and `WorkspaceToolProvider` renders it on the header line the model already
  reads the span from: `lines 1-5000 of 6000 — TRUNCATED: the window stops at this server's 5000-line
  read cap. Ask again with startLine 5001 to continue.`

**Deviations from the plan as written, all deliberate:**

1. **The advice lives in the reader, not the provider.** There is one case where "ask for the next
   window" is a lie — a single line longer than the byte cap, whose remainder no `startLine` can reach.
   Only the reader knows which case it is, so it composes the whole reason.
2. **The byte cap stops on a WHOLE line**, except the first line of a window, which is taken whatever its
   size and then clipped. A cut mid-line would hand back a `startLine` that skips the remainder of the
   line it cut; an empty window cannot be paged past at all.
3. **`PayloadBudget` moved from `Mcp.Application` to `Mcp.Contracts`.** The reader needs exactly that
   clipper (UTF-8 byte budget, never splitting a surrogate pair) for the case above, and a provider may
   not reference the catalog — so the shared half went to their common ancestor rather than being written
   twice and left to drift.
4. **The streaming half has no test with real teeth, and that is stated rather than papered over.** Every
   observable consequence is pinned (the caps, the marker, the true total, the next `startLine` actually
   working, an asked-for window never marked truncated); the difference between streaming and
   materializing is peak LIVE memory during an async read, which is sampling-only. See the *Tests*
   section of [../research/module_workspace_tools.md](../research/module_workspace_tools.md).

The original finding, kept for the record:

`src/Workspace.Infrastructure/SandboxedFileReader.cs:27` reads with `File.ReadAllLinesAsync` — the
whole file, whatever its size — and when a client omits `lineCount` the documented default is "read
to the end". Meanwhile `PayloadBudget.Apply` in `ToolCatalog.RecordAsync` (`ToolCatalog.cs:78-81`)
budgets **only the copy stored in telemetry**; the `ToolResult` returned at line 68 is untruncated.
So the byte budget the comments describe reads as a size ceiling and is not one: nothing bounds what
goes back over stdio or HTTP.

This was handed to the fix task as an optional item, because capping it is a **contract** decision
rather than a patch: a truncated read must say it was truncated and name the real total, or the
caller will page blindly. **It did NOT land there** — the fix task declined it for exactly that
reason and reported it instead. What it found, so this does not have to be rediscovered: a size
refusal alone is self-defeating, because the paging it would tell the caller to use goes through the
same `ReadAllLinesAsync`. Honouring the "page by number" contract on a file too large to hold means
reading it as a STREAM — skip to `startLine`, take `lineCount`, count the total on the way past —
which is a rewrite of the read path rather than a guard in front of it. The one line the fix task did
change here is the arithmetic that overflowed; the volume of a read is untouched.

**Fix (shipped):** cap lines and bytes in the reader itself; return an explicit truncation marker with
the true line count, so a caller can request the next window by number — the family's established
read-window contract.

### 2. `JsonDocument` is never disposed on the bridge's hot path — MEDIUM, **DONE 2026-08-16**

Closed by the fix task, in the same pass as the five CRITICAL/HIGH defects: `LocalLlmToolBridge`
parses into a `using var` and clones out of it, so the pooled buffers are returned. The `.Clone()`
stays — the element must outlive the document, which is what the clone is for; what was missing was
the `using`, not the copy. The identical shape at `src/Mcp.Contracts/ToolSchema.cs:23-24` is
untouched: it runs once at startup, as this plan already noted.

The original finding, kept for the record:

`src/Mcp.Bridge/LocalLlmToolBridge.cs:38-41`:

```csharp
arguments = JsonDocument.Parse(
    string.IsNullOrWhiteSpace(argumentsJson) ? "{}" : argumentsJson).RootElement.Clone();
```

`JsonDocument.Parse` rents from `ArrayPool<byte>.Shared`; undisposed, those buffers are never
returned and the pool's purpose is defeated on every bridge-driven call. The `.Clone()` then pays for
an independent copy that a `using` would make unnecessary. The same shape, once at startup and
therefore harmless, is at `src/Mcp.Contracts/ToolSchema.cs:23-24`.

**Fix (shipped):** `using var parsed = JsonDocument.Parse(...)`, cloning the root element out of it
before the document is disposed.

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
that a framework default you rely on is still a decision, and must be named. This compounds with the
catalog's ceiling, which the fix task shipped as `ToolCatalogOptions.CallTimeout` — **2 minutes**,
`TryAdd`-registered so a host may override it: between "the client disconnects" and "the call
finishes" there should be no unnamed gap, and these two numbers have to be chosen against each other.

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

1. ~~**(1) the read cap**~~ (done) — was the only HIGH here.
2. ~~**(2) `JsonDocument`**~~ (done) and **(3) budget cost** — small, same hot path.
3. **(4) explicit HTTP timeouts** — configuration, no behaviour change expected.
4. **(5) retention** — needs the operator decision; start the conversation early.
5. **(6) `Mcp.Ui`** and **(7) the sink note** — hygiene, any time.

## Test plan

Per `.claude/rules/shared/common/testing.md`, each behavioural item starts with a RED test named for
the guarantee, observed failing for the real symptom:

| item | test name |
|---|---|
| 1 | *(shipped)* `A_read_of_a_file_larger_than_the_cap_says_it_was_truncated_and_names_the_real_total` — RED: *"Expected content to start with `lines 1-5000 of 6000`, but `lines 1-6000 of 6000 …`"*, the whole file coming back untruncated. Plus `A_single_line_longer_than_the_byte_cap_is_clipped_instead_of_going_out_whole` — RED: the 2 MiB line back whole, no `TRUNCATED` in it. The streaming half is **not** covered; said so above and in the module doc |
| 2 | *(shipped without one)* — `ArrayPool.Shared` exposes no rent/return count, so the guarantee has no observable assertion short of instrumenting the pool; the change is `using` on a parse whose result is already cloned, and `SurfaceParityTests` covers the bridge path it sits on |
| 3 | `A_payload_already_within_budget_is_not_re_measured` |
| 5 | `A_day_folder_older_than_the_window_is_pruned_at_startup` |

Items 4, 6 and 7 have no observable behaviour to assert on their own — say so explicitly in the
summary when skipping, per the rule's Scope section.

Build, then run the test project's executable under `tests/.../bin/Debug/net10.0/` — never
`dotnet test`. If Smart App Control kills a freshly built runner with `0x800711C7`, retry the same
binaries ~60 s apart without rebuilding.

## Definition of Done

- [x] Item 1 is confirmed either closed by the fix task or implemented here — not assumed.
      *(Implemented here, 2026-08-16; suite 65 → 72 tests, 0 failed.)*
- [ ] Every other item is implemented, or explicitly declined here with the reason recorded.
- [ ] Each implemented behavioural item has a RED-then-GREEN test, both observations quoted.
- [ ] Timeouts and retention windows live in `appsettings.json`, not in code.
- [ ] `logs/` and the spool have a named retention owner, and the never-restarting case is answered.
- [ ] Nothing writes to stdout in the stdio host — the protocol contract still holds after the change.
- [ ] On completion the plan is promoted to `research/` with its deviations recorded, and the
      *Currently open* table in [README.md](README.md) is updated in the same task.
