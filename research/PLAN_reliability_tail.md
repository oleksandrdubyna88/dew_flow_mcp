# PLAN — the reliability tail the 24/7 audit left open

> Status: **IMPLEMENTED, 2026-08-16.** All seven items shipped here that day. The two things that held it
> open afterwards closed on 2026-08-17: item 6's consuming half landed in `dew_flow_rag_qln` (submodule pin
> moved and pushed, an `McpModule` publishing `/mcp`), and the midnight segment stopped being this
> repository's private divergence — it is in the shared rule and in all three siblings, the Rust sidecar
> included. Scope:
> `src/Mcp.Application`, `src/Mcp.Bridge`, `src/Mcp.Host`, `src/Mcp.Telemetry`, `src/ServiceDefaults`.
>
> **Deviations worth reading before the items.** Item 4's obvious fix was a trap: a global
> `AddRequestTimeouts` policy would have severed the MCP SSE stream on a schedule, so the policy is named
> and scoped to `/api/mcp`. Item 5's answer came from the operator and was neither candidate this plan
> offered — a file per run, segmented at UTC midnight. Item 7's proposed buffering was refused: it trades
> contention for losing the last lines of a crash.
>
> **And one recorded mistake.** Retention shipped twice. The first attempt invented `LogRetention.Prune`
> with `Mcp:Logs:RetentionDays` and a 30-day default, because the search for prior art covered only this
> repository — `dew_flow_benchmark` had already committed `Serilog:RetentionDays`, 14 days, and
> `PruneLogFolders(contentRoot, retentionDays, now)`. The second answer to a settled question is a mirror
> that has already drifted, which is the whole reason the rule is shared. Reverted to the family shape; the
> item text below has been corrected, and the reason lives beside the constant so the next reader meets it.
>
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
   section of [module_workspace_tools.md](module_workspace_tools.md).

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

### 3. The payload budget pays an O(n log n) cost on every call — MEDIUM, **DONE 2026-08-16**

Shipped as both halves of the fix below, plus one the finding did not name.

- **The cheap accept comes first.** `CertainlyFits(text.Length, budgetBytes)` returns before a single byte
  is counted. UTF-8 never spends more than **3** bytes per `char` — a 4-byte codepoint arrives as a
  surrogate PAIR, so it is 2 bytes per char, not 4 — which makes `chars * 3 <= budget` a proof that it
  fits. This is the hot path: two calls per tool call on payloads that almost always fit.
- **The search is one forward pass.** `LongestPrefixWithin` accumulates as it walks and returns the byte
  count with the prefix, so the whole operation costs **one** count instead of a binary search calling
  the counter afresh over a growing prefix (O(n log n)) and then a third full count to report the loss.
- **The surrogate pair is handled structurally**, not by a correction afterwards: the pair is consumed as
  one unit or not at all, so `WholeCharacterBoundary` is gone rather than reimplemented.

**Deviation:** the finding's proposed fix named the **wrong bound**, and that is worth recording because
it is an easy mistake to repeat. Characters bound bytes from *below* (`bytes >= chars`), which proves only
the reject case — `chars > budget` means it certainly overflows. What proves "already fits" is the upper
bound above. Corrected in the text below before the work started.

**Tests:** `A_payload_the_cheap_check_accepts_is_returned_whole_and_reports_no_loss` (the observable half
— "it did not count" is not observable from outside), and
`A_payload_is_never_returned_over_its_budget_whatever_it_is_made_of`, which is the safety property a wrong
bound would break: five alphabets at 1, 2, 3 and 4 bytes per codepoint × 45 budgets, asserting the result
never overflows, that every byte is kept or counted, and that the cut is a prefix rather than a
re-encoding.

The original finding, kept for the record:

### 3-original. The payload budget pays an O(n log n) cost on every call

`src/Mcp.Contracts/PayloadBudget.cs:24-39` runs `Encoding.UTF8.GetByteCount(text)` unconditionally —
the fits-the-budget early return at `:32` is reached only *after* the full count is paid — and
`LongestPrefixWithin:44-68` binary-searches with a fresh `GetByteCount` over a growing prefix each
step. It is called twice per tool call from `ToolCatalog.RecordAsync`
(`src/Mcp.Application/ToolCatalog.cs:155-158`) — for arguments and for the response. On small payloads
this is noise; composed with item 1 it becomes a real per-call cost on the hot path, paid to produce a
record that will be truncated anyway.

*(The type moved to `Mcp.Contracts` when item 1 shipped — deviation 3 above. This finding was written
against its old home in `Mcp.Application`.)*

**Fix:** short-circuit on `text.Length` before counting, and avoid re-counting from zero inside the
search. **The cheap check must use the UPPER bound, and the original wording of this item named the
wrong one:** chars bound bytes from *below* (`bytes >= chars`), which proves only the reject case —
`text.Length > budgetBytes` means the payload certainly overflows. What proves "already fits" without
counting is that UTF-8 never spends more than 3 bytes per `char` (a 4-byte codepoint is two chars), so
`text.Length <= budgetBytes / 3` is the accept. Both are worth having; only the second removes the
count from the hot path's common case.

### 4. No explicit Kestrel or request timeouts on the HTTP transport — MEDIUM, **DONE 2026-08-16**

Three numbers, all in [appsettings.json](../src/Mcp.Host/appsettings.json) with the reasoning beside them:
`Kestrel:Limits:KeepAliveTimeout` 2 min, `Kestrel:Limits:RequestHeadersTimeout` 30 s (the slowloris
bound), and `Mcp:Api:RequestTimeout` 30 s. Both Kestrel values happen to equal the framework's own
defaults today, and that is the point — they are now written down, reviewable, and changeable without a
rebuild.

**Deviation, and the trap the plan did not see: there is NO global request-timeout policy, deliberately.**
`MapMcp()` serves a Server-Sent Events stream that is meant to stay open, and the obvious
`AddRequestTimeouts` default would have severed the MCP transport every N seconds — a self-inflicted
outage introduced by a reliability fix. The policy is named (`McpApiEndpoints.TimeoutPolicy`) and scoped
to the `/api/mcp` group, which answers in milliseconds. A tool call stays bounded by
`ToolCatalogOptions.CallTimeout` at the one dispatch chokepoint, and the two numbers are chosen against
each other: raising the transport past the catalog's ceiling would only mean waiting on a call the server
has already given up on.

**No test**, per this plan's own Scope note — there is no observable behaviour to assert without an
ASP.NET test host this repository does not have. Verified instead by running the host: it starts with the
configuration bound, no binding error in the log, and both `/api/mcp/health` and `/api/mcp/surface`
answer.

The original finding, kept for the record:

### 4-original. No explicit Kestrel or request timeouts on the HTTP transport

`src/Mcp.Host/Program.cs:76-87` builds the web host with no `KestrelServerOptions` and no
`RequestTimeouts` middleware, relying entirely on framework defaults. *(Was `:36-47` when this was
written; the surface-configuration work of 2026-08-16 added a `--print-surface` branch above it. The
finding is unchanged — those twelve lines still name no timeout.)* The shared rule is explicit
that a framework default you rely on is still a decision, and must be named. This compounds with the
catalog's ceiling, which the fix task shipped as `ToolCatalogOptions.CallTimeout` — **2 minutes**,
`TryAdd`-registered so a host may override it: between "the client disconnects" and "the call
finishes" there should be no unnamed gap, and these two numbers have to be chosen against each other.

**Fix:** configure keep-alive, request-header and request-body timeouts explicitly, in
`appsettings.json` rather than in code, so an operator can change them without a rebuild.

### 5. Log and spool files grow without bound for a process that never restarts — MEDIUM, **DONE 2026-08-16**

**The operator decided, and it is the third option neither the plan nor its two candidates named: keep
one file per run, and segment it at midnight.** A run starting at 15:00 writes
`logs/2026-08-16/mcp-15-00-00-1234.log`; at 00:00 it continues in
`logs/2026-08-17/mcp-00-00-00-1234.log`. Same shape for the spool, `.jsonl`.

Why this is better than either candidate the plan offered. A size-based rollover within the run bounds
the file but produces segments that line up with nothing — you cannot tell from a name which hours it
covers. A planned periodic restart moves the problem onto a human's discipline, and the whole premise of
the audit was surviving a night nobody is watching. Segmenting on the CLOCK bounds every file to one day,
keeps the run identifiable (same pid on consecutive days), and puts each file in the folder it belongs to
for free — which the previous arrangement broke the moment a run crossed midnight, since the file stayed
in the folder of the day it *started*.

It is also **not** the rolling-by-day sink the family rule forbids. That rule's objection is that rolling
by day merges every run into one file; these files belong to one run and say so by pid.

What shipped:

- `DailyRunFileSink` (`src/ServiceDefaults`) — owns *which file*, delegates the writing to Serilog's own
  file sink per segment rather than reimplementing encoding and flushing. `McpLogging.RunFilePath`
  delegates to it, so the path shape has one definition.
- `SpoolUsageSink.PathFor(record.At)` — keyed on the RECORD's timestamp, not the writer's clock: a call
  at 23:59:59 drained at 00:00:01 belongs to the day it happened, which is the day anyone looking for it
  will search.
- **Forward only**, in both. An event stamped earlier than the open segment — a clock correction, or an
  event overtaken by the boundary — lands in the open file rather than reopening yesterday's and
  orphaning today's. A late line in the right run beats a lost file.
- The inner per-segment logger is `MinimumLevel.Verbose`, because everything reaching it has already
  passed the outer filter and an inner default of Information would silently drop the Debug lines
  somebody raised the level to see.

**Tests:** `A_run_that_outlives_the_day_continues_in_a_midnight_segment_under_the_next_days_folder`,
`Each_segment_holds_only_its_own_days_events`,
`Both_segments_carry_the_same_pid_so_the_run_is_still_one_thing`,
`An_event_stamped_before_the_open_segment_does_not_reopen_the_previous_day`, and
`A_spool_that_outlives_the_day_continues_in_a_midnight_segment`. All watched failing against the old
shape before being trusted — "found 1" file where two were expected.

**The other half — a retention window — shipped too**, after the operator asked for it separately.
`McpLogging.PruneLogFolders` deletes day folders older than `Serilog:RetentionDays` (default **14**), once,
at startup rather than on a timer: a background sweep is a second thing that can fail silently in a process
nobody is watching, and the window is measured in days. Zero keeps everything — an explicit off switch,
because a misread config that silently deleted a month of logs would be the worst possible failure of a
retention feature. Only folders whose name parses as `yyyy-MM-dd` are candidates, and one that will not
delete is counted rather than thrown, because a log viewer holding a file open must not stop a host from
starting.

**It shipped WRONG first, and the correction is the point.** The original was `LogRetention.Prune` with
`Mcp:Logs:RetentionDays` and 30 days — a whole second answer to a question `dew_flow_benchmark` had already
settled in commit `e9a52aa` with the key, default and signature above. The search for prior art had covered
this repository only. Deleted and replaced with the family shape verbatim; the reason sits in the XML doc
beside the constant, because the next person to add retention to a fourth repository will search the same
way.

**The spool is deliberately NOT pruned here, and that is the answer rather than an omission.** It looks
like the same problem and is not: a spool file is *drained* by a consumer, and this process cannot know
which records that consumer has taken. `dew_flow_benchmark` already owns `bench telemetry prune --spool
<dir> --older-than <days>` beside its ingester. Deleting on its behalf would destroy telemetry nobody
ingested — a worse bug than the growth it fixes. The owner is named; that is what the audit asked for.

**Tests:** `A_day_folder_older_than_the_window_is_pruned_at_startup` — the name this plan reserved —
plus the boundary day, the off switch, a non-date folder, a missing `logs/`, and a folder held open.
Watched failing against a sweep that expires nothing: *"Expected removed to be 1, but found 0"*. Verified
live: a planted `logs/2020-01-01` was gone after one start.

**And this changed a rule shared by four repositories — reconciled 2026-08-17.**
`.claude/rules/shared/common/logging-serilog.md` (the `dew_flow_conventions` submodule) said *"A file per
RUN, not per day"* and *"Never a rolling-by-day file sink"*, and only this repository had the segment. The
rule now carries the distinction explicitly — rolling by day merges DIFFERENT runs, a segment splits ONE —
and every sibling has it: `BenchLogging` (`dew_flow_benchmark` `3f8ada8`), `RagLogging`
(`dew_flow_rag_qln` `94a3b81`), and `DaySegments` in the Rust sidecar (`48e544b`), which needed its own
implementation because `tracing` has no Serilog.

The sidecar also gained the retention half it had never had (`bd008f4`) — it is the host least likely to
be restarted, started once by the orchestrator and serving until the machine does not, so it was the worst
possible one to leave unbounded. Its day-folder names are validated by round-tripping them through the
calendar rather than compared as strings, so a `2026-02-30` left in `logs/` by a person survives a sweep
that a lexicographic comparison would have taken.

The original finding, kept for the record:

### 5-original. Log and spool files grow without bound for a process that never restarts

`src/ServiceDefaults/McpLogging.cs:76-80` writes one file per run with no `fileSizeLimitBytes` and no
`retainedFileCountLimit`; `src/Mcp.Telemetry/SpoolUsageSink.cs:150-157` fixes one spool path at
construction for the life of the process. Both are correct per the family's "a file per RUN" rule —
but that rule's mitigating rotation *is* the restart, and this deployment's premise is that the
process does not restart. The two facts have never been reconciled.

**Fix:** name the owner, per `.claude/rules/shared/common/logging-serilog.md` § Retention: prune day
folders older than the window at startup, **and** decide what a genuinely months-long process does —
a size-based rollover within the run, or a planned periodic restart recorded in the README. This is
the one item on this list that needs an operator decision rather than a patch.

### 6. `Mcp.Ui` is built and shipped but wired to nothing — LOW, **DONE 2026-08-16**

**Wired, not removed** — the operator's call. What shipped on this side:

- **`Pages/McpSurface.razor` (+ `.razor.cs`), route `/mcp`** — what this server is actually advertising:
  every tool with the exact description text served, the schema hashes, the surface hashes, the build,
  and the components behind it. It is the human-readable face of the `SurfaceFingerprint` that
  [PLAN_tool_surface_config.md](PLAN_tool_surface_config.md) shipped the same day; before
  this page, "declare and echo" meant curling an endpoint and reading JSON.
- **`Services/McpConsoleApi`** — the read side, where every failure becomes a value. `Read<T>` carries
  the value, whether it arrived, and why it did not, so "the daemon could not be reached" and "this
  server advertises no tools" render as two different messages rather than one blank table.
- **`AddMcpUi()`** — one registration call, registering the read side and nothing else.

  **This shipped wrong first and the correction is instructive.** It was `AddMcpUi(Uri apiBaseAddress)`,
  written without looking at how the consuming console supplies an address — and a fixed `Uri` cannot
  express what the mount point actually needs. The WASM client takes its address once from
  `HostEnvironment.BaseAddress`; the SSR host must take it **per request**, because a server-side render
  calls back into the process serving it. So the address belongs to the host's own `HttpClient`
  registration, exactly as `RagConsoleApi` already did it, and the slice takes it by injection.
- **`src/Mcp.Ui/README.md`** — who mounts it, and why `Mcp.Host` deliberately does not.

**Deviation:** the item offered "either wire it or remove it from the shipped set". Neither happened
*inside this repository*, and that is the point — `Mcp.Host` is the standalone server, and giving it a
Blazor console would make the clone-and-run bar heavier for a screen no CLI user opens. The mount point
is the product console in `dew_flow_rag_qln`, whose `Daemon.Client.csproj` already referenced this
project and whose comment already said *"Mcp.Ui from the public MCP package"*. The wiring was waiting on
this project having a page.

**Tests:** `McpConsoleApiTests` — the absent-versus-empty distinction in both directions, an unreachable
daemon, an unparseable body, a timeout, and "nothing asked yet" as its own state. **The markup is not
covered**: this repository has no bUnit harness and adding one is its own decision, said here and in the
project's README rather than left as an assumed gap.

**The consuming half landed 2026-08-17** in `dew_flow_rag_qln`, after the pin was pushed. It went through
that repository's `IDaemonModule` registry rather than as five edits to the composition root: an
`McpModule` owns `MapMcpApi()` — which had been hardcoded ABOVE the module loop, the very shape the
contract exists to remove — and declares its pages assembly and its `("MCP", "/mcp")` entry, so the router
slice, the nav entry, the AppHost URL and the prerender list all come from one declaration.

Verified live rather than by inspection: the running daemon published `{"name":"MCP","url":".../mcp"}` in
its endpoint file with nobody editing the publish code, and `EveryPublishedPageRenders` — which GETs every
published page and fails on a 500 — ran green with **`skipped: 0`**, the part that matters, since that test
skips itself when no console is answering.

The original finding, kept for the record:

### 6-original. `Mcp.Ui` is built and shipped but wired to nothing

`dew_flow_mcp.slnx:10` includes it, `Mcp.Ui.csproj` builds it, and `Mcp.Host.csproj` does not
reference it. `ArchitectureTests.Every_shipped_project_is_covered_by_the_rule` already treats it as
shipped. It is inert scaffolding — no runtime risk, but a live-looking surface that is not one is a
trap for the next reader, and this repository is public.

**Fix:** either wire it or remove it from the shipped set; if it is deliberate scaffolding for
planned work, say so in its own README so the next reader stops looking for the wiring.

### 7. The console sink flushes under a global lock, per line — LOW, **DONE 2026-08-16**

Fixed rather than merely noted. The line is now rendered **outside** the lock and written **once**
inside it; the lock stays, because a line is only atomic if something makes it so.

The finding understated the cost. It described one flush per event, but `Console.Out` auto-flushes, so
the five to seven separate `Write` calls were five to seven flushes — each held against every other
thread, with the formatter's work inside the same critical section.

**Deviation:** the plan's fix was *"know it before turning the level up; if it bites, buffer and flush on
an interval"*. Buffering was **not** taken, deliberately: it would trade contention for losing the last
lines of a crash, and a host that dies while wiring itself up is exactly when the log matters. Shrinking
the critical section to one write gets the win with none of that. The sink also gained an optional
`TextWriter`, which is what makes it testable without touching process-global `Console` state — a shared
static in a parallel suite is a defect waiting for a second test.

**Tests:** `One_event_reaches_the_stream_as_a_single_write`,
`An_exception_still_travels_with_its_line_in_that_one_write`,
`The_level_is_coloured_even_though_the_stream_is_redirected` (the measured reason this sink exists at
all), and `Concurrent_events_never_interleave_within_a_line` — which also covers the one risk the change
introduces, since the formatter is now called concurrently. Teeth verified by restoring the old shape:
**5 writes** where 1 was expected, 9 with an exception, and the concurrency case produced **801 lines out
of 800** — a genuinely torn line.

The original finding, kept for the record:

### 7-original. The console sink flushes under a global lock, per line

`src/ServiceDefaults/AnsiConsoleSink.cs:52-69` holds a lock across `writer.Flush()` for every event.
Correct for interleaving safety, and invisible at Information volume — but it becomes a contention
and latency point exactly when someone raises verbosity to debug a live problem, which is the moment
it must not.

**Fix:** know it before turning the level up; if it bites, buffer and flush on an interval rather
than per line. Listed so the next person debugging a hot server does not discover it by feel.

## Build order

1. ~~**(1) the read cap**~~ (done) — was the only HIGH here.
2. ~~**(2) `JsonDocument`**~~ and ~~**(3) budget cost**~~ (done) — small, same hot path.
3. ~~**(4) explicit HTTP timeouts**~~ (done) — configuration; no behaviour change, and one avoided
   (a global policy would have severed the SSE stream).
4. ~~**(5) retention**~~ (done) — the operator's answer was a midnight segment, not either candidate.
   Its *other* half, pruning old day folders, followed on a second instruction.
5. ~~**(6) `Mcp.Ui`**~~ and ~~**(7) the sink**~~ (done). Item 6's consuming half landed in
   `dew_flow_rag_qln` on 2026-08-17 once the pin was pushed.
6. ~~**the family reconciliation**~~ (done, 2026-08-17) — not a numbered item, because it only became work
   once item 5 chose an answer that no other repository had. The shared rule, the two .NET siblings and the
   Rust sidecar all carry the midnight segment now, and the sidecar gained retention as well.

## Test plan

Per `.claude/rules/shared/common/testing.md`, each behavioural item starts with a RED test named for
the guarantee, observed failing for the real symptom:

| item | test name |
|---|---|
| 1 | *(shipped)* `A_read_of_a_file_larger_than_the_cap_says_it_was_truncated_and_names_the_real_total` — RED: *"Expected content to start with `lines 1-5000 of 6000`, but `lines 1-6000 of 6000 …`"*, the whole file coming back untruncated. Plus `A_single_line_longer_than_the_byte_cap_is_clipped_instead_of_going_out_whole` — RED: the 2 MiB line back whole, no `TRUNCATED` in it. The streaming half is **not** covered; said so above and in the module doc |
| 2 | *(shipped without one)* — `ArrayPool.Shared` exposes no rent/return count, so the guarantee has no observable assertion short of instrumenting the pool; the change is `using` on a parse whose result is already cloned, and `SurfaceParityTests` covers the bridge path it sits on |
| 3 | *(shipped)* `A_payload_the_cheap_check_accepts_is_returned_whole_and_reports_no_loss` — the observable half; "it did not count" cannot be seen from outside. Plus `A_payload_is_never_returned_over_its_budget_whatever_it_is_made_of`, the safety property a wrong bound breaks: five alphabets × 45 budgets |
| 5 | *(shipped)* `A_run_that_outlives_the_day_continues_in_a_midnight_segment_under_the_next_days_folder`, `Each_segment_holds_only_its_own_days_events`, `Both_segments_carry_the_same_pid_so_the_run_is_still_one_thing`, `An_event_stamped_before_the_open_segment_does_not_reopen_the_previous_day`, `A_spool_that_outlives_the_day_continues_in_a_midnight_segment` — RED against the old shape: *"Expected … to contain 2 item(s), but found 1"*. Plus `A_day_folder_older_than_the_window_is_pruned_at_startup` — the name this plan reserved — with the boundary day, the off switch, a non-date folder, a missing `logs/` and a folder held open; RED against a sweep that expires nothing: *"Expected removed to be 1, but found 0"* |
| 7 | *(shipped)* `One_event_reaches_the_stream_as_a_single_write`, `An_exception_still_travels_with_its_line_in_that_one_write`, `The_level_is_coloured_even_though_the_stream_is_redirected`, `Concurrent_events_never_interleave_within_a_line` — RED against the old shape: *"Expected stream.Writes to be 1, but found 5"*, and 801 lines out of 800 |

Item 4 has no observable behaviour to assert without an ASP.NET test host this repository does not have,
and item 6 has none of its own — said explicitly here rather than skipped quietly, per the rule's Scope
section. Item 4 was verified by running the host instead: it starts with the configuration bound and both
management endpoints answer.

Build, then run the test project's executable under `tests/.../bin/Debug/net10.0/` — never
`dotnet test`. If Smart App Control kills a freshly built runner with `0x800711C7`, retry the same
binaries ~60 s apart without rebuilding.

## Definition of Done

- [x] Item 1 is confirmed either closed by the fix task or implemented here — not assumed.
      *(Implemented here, 2026-08-16; suite 65 → 72 tests, 0 failed.)*
- [x] Items 2, 3, 4, 5, 6 and 7 are implemented, with their deviations recorded above. Item 6's
      consuming half — the nav entry, the router slice, the AppHost URL — lives in `dew_flow_rag_qln`
      and is gated on the submodule pin.
- [x] Item 5's answer came from the operator, and it was neither candidate the plan offered: a file per
      run, segmented at UTC midnight. Its other half — pruning day folders — shipped after, on a second
      instruction, and the spool's owner is named rather than assumed.
- [x] Each implemented behavioural item has a RED-then-GREEN test, both observations quoted — items 3,
      5 (segments and retention) and 7. Item 4 has no observable behaviour without an ASP.NET test host,
      and item 6's markup none without a bUnit harness; both said so explicitly rather than skipped.
- [x] Timeouts **and the retention window** live in `appsettings.json`, not in code — four values, each
      with its reasoning beside it.
- [x] `logs/` and the spool have a named retention owner, and the never-restarting case is answered. The
      log: a midnight segment plus a 14-day startup sweep, both here. The spool: a midnight segment here,
      and its total owned by the INGESTER, because only the consumer knows which records it has taken.
- [x] Nothing writes to stdout in the stdio host — verified by running it: the surface probe puts JSON
      there and every log line goes to stderr.
- [x] The family reconciliation the midnight segment created is done, in four repositories: the shared
      rule, `BenchLogging`, `RagLogging`, and `DaySegments` in the Rust sidecar — which needed a separate
      implementation, since `tracing` is not Serilog and only the CONTRACT is shared.
- [x] On completion the plan is promoted to `research/` with its deviations recorded, and the
      *Currently open* table in [../todo/README.md](../todo/README.md) is updated in the same task.
      *(Done 2026-08-17. Held back on 2026-08-16 deliberately: promoting while this repository logged
      differently from the rule it ships with would have filed an open divergence as documentation.)*
