# PLAN — the tool surface as configuration: descriptions from files, a subset at startup, and a surface a caller can read back

> Status: **plan only, nothing implemented yet.** Scope: `src/Mcp.Application` (two decorators and a
> catalog), `src/Mcp.Contracts` (one wire record), `src/Mcp.Telemetry` (one additive field),
> `src/Mcp.Host` (four flags), `src/Mcp.Api` (one endpoint), `tests/Mcp.Tests`. **No change inside
> `ToolCatalog`, `ToolSchema`, `IToolProvider`, `CatalogToolFunction` or `LocalLlmToolBridge`.**
>
> Sibling half: `dew_flow_benchmark · todo/PLAN_tool_benchmark.md` — the harness that consumes this. A
> change that crosses the boundary is named in both plans.
>
> Related: [PLAN_mcp_product.md](PLAN_mcp_product.md) — this plan is the machinery its
> *"the thing this repository actually sells"* section asks for;
> [../research/PLAN_usage_telemetry.md](../research/PLAN_usage_telemetry.md) (the emitter this extends);
> [../research/telemetry_v0_wire.md](../research/telemetry_v0_wire.md) (the schema, owned by the benchmark).

## 1. The goal, before any solution

[PLAN_mcp_product.md](PLAN_mcp_product.md) already states the thesis, and states it as settled:

> **The lesson is not "write nicer docs". It is that a tool's description is a measured artefact**, and a
> change to it is an arm of the experiment matrix like any retrieval change.

That sentence cannot currently be acted on. A description is a C# string literal compiled into the binary
(`src/Workspace.Application/WorkspaceToolProvider.cs:19-22`), and the catalog is assembled once at startup
from every registered provider with no way to serve fewer (`src/Mcp.Application/ToolCatalog.cs:61-72`).
So A/B-ing a wording means a branch and a rebuild, and A/B-ing a tool subset is not possible at all. A
matrix of ten wordings is, in practice, not runnable.

The evidence that makes this worth building rather than a nicety, measured on the previous generation and
carried in `dew_flow_benchmark · research/MEASURED_LESSONS.md`:

- Holding the tools fixed and rewriting **one instruction about which tool to use when** moved a score
  **16.5 points of 63**; swapping the toolbox from 4 tools to 18 moved it **1**.
- The **same four tools behind a differently-shaped surface** scored **4/63 against 36/63** — nine times,
  from the form alone.
- Given no such surface, two models opened every code task with guessed-identifier grep and searched in
  natural language **not once in 37 searches**; on an MCP surface the same models wrote full behavioural
  sentences.

A description is therefore not documentation wrapped around the product. On this evidence it *is* a
larger part of the product than the tool count is, and it is the only part currently unmeasurable.

**And this is not a benchmark feature.** Every capability below is one a customer wants for its own sake:
serve an agent a smaller surface for a focused task, tune wording without shipping a binary, and read back
what a running server is actually advertising. Nothing in this plan names a benchmark, a leg, or a lane.

## 2. What exists today, verified

| capability | state | where |
|---|---|---|
| A tool's description | a `required string` on a record, authored as a C# literal in the provider | `src/Mcp.Contracts/ToolSchema.cs:12`, `src/Workspace.Application/WorkspaceToolProvider.cs:19-22` |
| The catalog | built once from `IEnumerable<IToolProvider>`, flattened, name-ordered, frozen; duplicate names throw at construction, naming both providers | `src/Mcp.Application/ToolCatalog.cs:46-72, 199-214` |
| Serving a subset per session or per call | **does not exist** — `Advertised` is one process-wide immutable list | `src/Mcp.Application/ToolCatalog.cs:64, 72` |
| The two presentations | protocol and bridge, both projections of `catalog.Advertised`; the bridge declares no tool of its own | `src/Mcp.Bridge/LocalLlmToolBridge.cs:16-18`, `src/Mcp.Server/CatalogToolFunction.cs` |
| Parity between them | asserted, including *a new provider reaches both surfaces with no edit inside the MCP module* | `tests/Mcp.Tests/SurfaceParityTests.cs:21, 32, 47, 83` |
| Host configuration | `--root` and `--spool` read once at process start via one `ReadOption` helper; `AddToolStack` is the single wiring point | `src/Mcp.Host/Program.cs:18-19, 49-61` |
| Registration | `AddMcpApplication()` takes no arguments; every service is `TryAdd`, so a host may substitute | `src/Mcp.Application/McpApplicationExtensions.cs:11-25` |
| Telemetry record | `telemetry/v0`, one JSONL line per call, byte-budgeted payloads, three-state outcome — and **no correlation field** | `src/Mcp.Telemetry/TelemetryRecord.cs:14-29` |
| The consumer of that record | **already reads `correlation` and treats its absence as unattributed**, with a comment saying the field is additive within v0 | `dew_flow_benchmark · src/Bench.Application/TelemetryCodec.cs:123-128, 191` |
| Management API | one endpoint, `GET /api/mcp/health` | `src/Mcp.Api/McpApiEndpoints.cs` |

**The telemetry asymmetry is the cheapest item in this plan.** The reader was built expecting a field the
writer never writes, and was built to keep reading lines that lack it. Adding it costs one record member
and breaks nothing on either side.

## 3. The shape — decisions

### 3.1 Descriptions come from files, with the compiled literal as the floor

```csharp
// src/Mcp.Application/ToolDescriptionCatalog.cs
public sealed class ToolDescriptionCatalog(string directory)
{
    /// Empty or missing -> the caller's built-in text. A set is a subfolder; "" reads the root.
    public string DescriptionFor(string toolName, string set, string builtIn);
}
```

Layout, mirroring how prompt catalogs are laid out elsewhere in the family:

```
tool-descriptions/
  concise-v1/rt_read_local_file.md
  behavioural-v1/rt_read_local_file.md
  <set>/<tool-name>.md
```

Three rules, each of which is a decision:

- **Never empty.** A file that is missing, unreadable or blank yields the compiled literal. A tool with
  no description is a tool no agent can route to, and an empty file is a far more likely accident than a
  deliberate silence.
- **The literal stays the source of truth for the shipped default.** Files override; they do not replace.
  A customer who names no catalog gets exactly today's binary, byte for byte.
- **Read once, at startup.** The description is part of the published contract for the life of a process;
  a description that changes mid-session would make one session's traffic two populations. Re-reading is
  a restart, which for a subprocess-per-task client is free.

### 3.2 One decorator does both jobs, outside the dispatch core

```csharp
// src/Mcp.Application/ToolSurfaceProvider.cs
internal sealed class ToolSurfaceProvider(
    IToolProvider inner,
    IReadOnlySet<string> allowed,          // empty = every tool the inner provider offers
    ToolDescriptionCatalog? descriptions,
    string descriptionSet) : IToolProvider
{
    public IReadOnlyList<ToolSchema> Tools { get; }   // filtered, then `with { Description = … }`
    public string Scope => inner.Scope;
    public Task<ToolResult> InvokeAsync(ToolCall call, CancellationToken ct);  // refuses a tool it does not advertise
}
```

Applied to the registered providers **before** `ToolCatalog` is constructed. That placement is the whole
design:

- `ToolCatalog`, `ToolSchema`, `IToolProvider`, `CatalogToolFunction` and `LocalLlmToolBridge` are not
  touched, so the parity guarantee holds by construction rather than by a second assertion — both
  presentations project the same `Advertised` list they always did, and that list is simply now
  configurable. `SurfaceParityTests.cs:83`'s *"a new provider reaches both surfaces with no edit inside
  the MCP module"* is exactly the property being leaned on.
- `ToolSchema` is a record, so a description override is `with { Description = text }` — no mutation, no
  new type, no second schema shape to keep in sync.
- **A tool outside the subset is refused, not failed.** It never appears in `Advertised`, so a caller
  reaching for it is working from a stale configuration — the same reading a sandbox denial gets, and
  `ToolResult.Refusal` is the state that says so. `ToolCatalog` already meters an unknown tool
  deliberately (`ToolCatalog.cs:78-88`), and that stays true here: *"a client repeatedly asking for a
  tool this server does not advertise is a fact about the surface"*.

`AddMcpApplication` gains two optional parameters (`toolSubset`, `descriptions` + `descriptionSet`), all
defaulted, so every existing call site — including all of `Mcp.Tests` — compiles and behaves identically.
The wrapping happens only when something was actually supplied.

**Refuse a configuration that does not fit.** A description set naming a tool the subset excludes, or a
subset naming a tool no provider offers, stops the host at startup naming both sides. A surface silently
smaller than the one somebody configured is the failure this whole plan exists to make visible; it must
not be introducible by a typo.

### 3.3 A server can be asked what it is actually serving

```csharp
// src/Mcp.Contracts/SurfaceFingerprint.cs
public sealed record SurfaceFingerprint(
    IReadOnlyList<ToolDescriptionEcho> Tools,   // name + the exact description text served + schema hash
    string DescriptionSet,
    string ToolsHash,                            // over the ordinal-sorted names
    string DescriptionsHash,                     // over name=text pairs, ordinal-sorted
    string App, int Pid, DateTimeOffset BuiltAt);
```

Two ways to read it, because there are two shapes of host:

- **`Mcp.Host --print-surface`** — writes the fingerprint as JSON to stdout and exits. Works for the
  stdio host, needs no port, and is the natural instrument for a startup check or a CI assertion.
- **`GET /api/mcp/surface`** — the same record from a running HTTP host, beside the existing health
  endpoint.

This is the discipline of *declare and echo, never assume*, which the family already applies to retrieval
engines: what a configuration asked for and what a process is serving are two different facts, and only
the second one explains a result. It is also, for a customer, the answer to "which build is this and what
is it advertising" — a question that currently requires reading the binary.

**The hash is computed here and quoted, never re-derived by a consumer.** A second implementation of a
canonicalisation is two implementations that must agree byte for byte forever; consumers store and
compare the string this server printed.

### 3.4 Telemetry carries the correlation its reader already expects

`TelemetryRecord` gains one member, `correlation`, holding a caller-declared leg and phase in the same
`captured / value / reason` wire shape everything else uses. It is **additive within `telemetry/v0`** —
the consumer already defaults an absent object to unattributed and documents that it must
(`dew_flow_benchmark · src/Bench.Application/TelemetryCodec.cs:123-128`), so no version bump and no
rewrite of anything already spooled.

- **It is what the caller said**, never something this server infers. An MCP server has no idea what a
  benchmark leg is, and giving it one would make the surface unshippable to anyone not running that
  harness. A real session declares nothing and reads as unattributed, which is the truth.
- **`--correlation <id[/phase]>` is a process-level flag, and is honest only for a process serving one
  unit of work** — a stdio server launched per task. The shared HTTP transport must not use it: one
  value stamped across concurrent callers would invent an attribution, which is worse than having none.
  The flag is refused when combined with the HTTP transport, rather than quietly applied.

### 3.5 What stays out

- **No per-request tool filtering, no session-scoped catalog.** The stdio server is a subprocess per
  client and the HTTP server is configured per deployment; process-start configuration covers both, and
  a per-session catalog is a large mechanism for a case a restart already answers. If a real need appears
  later it is a new plan, not a field bolted onto this one.
- **No hot reload of descriptions.** §3.1's reasoning: a contract that changes under a live session is
  two populations in one log.
- **No editing tools, and no knowledge of retrieval.** The boundary in
  [PLAN_mcp_product.md](PLAN_mcp_product.md) Phase 4 is untouched; nothing here names a tool family.
- **No authentication work.** The HTTP transport is still open, still called out in
  [PLAN_mcp_product.md](PLAN_mcp_product.md) Phase 2, and still not solved by this plan — noted so that
  a new endpoint here is not mistaken for a security review.

## 4. Build order

1. **`ToolDescriptionCatalog`** — file resolution, sets as subfolders, never-empty floor, literal
   fallback. Pure, no DI, unit-tested against a temp directory.
2. **`ToolSurfaceProvider` + `AddMcpApplication` overload** — subset filtering, description override,
   refusal of a tool outside the surface, startup refusal of a configuration that does not fit.
3. **Host flags** — `--tools`, `--descriptions`, `--description-set`, using the existing `ReadOption`
   helper and the existing `AddToolStack` seam.
4. **`SurfaceFingerprint` + `--print-surface` + `GET /api/mcp/surface`** — the echo, both shapes.
5. **`correlation` on `TelemetryRecord`, and `--correlation`** — additive field, process-level stamp,
   refused on the HTTP transport.
6. **Documentation** — `research/module_tool_dispatch.md` and `research/module_telemetry.md` updated,
   `research/architecture.md`'s configuration section extended, `todo/README.md` table refreshed.

Steps 1–3 are one shippable unit if that reads better in review; 4 and 5 are independent of each other and
of 1–3.

## 5. Test plan

xUnit v3 executables only, never `dotnet test`.

- **Catalog**: a named set overrides; a missing file falls back to the literal; a **blank** file falls
  back rather than serving an empty description; an unknown set is refused by name rather than silently
  serving defaults.
- **Subset**: only the named tools are advertised; a call to an excluded tool is **refused** (not failed,
  not dispatched); an empty subset means every tool — proving the default path is untouched.
- **Parity, extended**: after a subset and a description set are applied, the protocol surface and the
  bridge still advertise identical names and byte-identical schemas, and identical description text. This
  is the existing guarantee under the new configuration, and it is the test that would catch a decorator
  applied to one presentation and not the other.
- **Startup refusal**: a description set naming a tool outside the subset stops the host, and the message
  names both the set and the tool.
- **Fingerprint**: `--print-surface` emits valid JSON and exits zero; the hashes change when a
  description changes and are stable across two runs of an unchanged configuration; the echoed text is
  the text actually advertised, asserted against `catalog.Advertised` rather than against the file.
- **Telemetry**: a record with a correlation round-trips; a record without one is still valid
  `telemetry/v0`; the fixture line already committed in the benchmark's tests still reads unchanged.
- **`--correlation` with the HTTP transport** is refused at startup with a reason.
- Every defect found while building gets a RED test first, watched failing for the real symptom.

## 6. Definition of Done

- [ ] A tool's description can be changed without recompiling, and the compiled literal remains the
      default when no catalog is named.
- [ ] A server can be started with a subset of its tools, and a tool outside that subset is refused
      rather than dispatched.
- [ ] `ToolCatalog`, `ToolSchema`, `IToolProvider`, `CatalogToolFunction` and `LocalLlmToolBridge` are
      unchanged — the diff proves the seam was sufficient.
- [ ] Both presentations still advertise byte-identical schemas **under configuration**, asserted.
- [ ] `--print-surface` and `GET /api/mcp/surface` report exactly what is advertised, with hashes.
- [ ] A configuration that does not fit stops the host at startup, naming both sides.
- [ ] `telemetry/v0` carries an optional correlation; lines without one still read; the benchmark's
      committed fixture is unaffected.
- [ ] Nothing in this repository names a benchmark, a leg, a lane, or retrieval.
- [ ] `research/` module docs and `todo/README.md` updated.

## 7. Open questions

1. **Should the argument JSON Schema be a set-able artefact too**, alongside the description? A schema
   whose parameters carry their own descriptions is plausibly as large an effect as the prose, and
   nothing has measured it. It would be the same mechanism one field wider — deliberately deferred until
   the description axis has produced a number.
2. **A version stamp on a description set.** Today a set is a folder name; two machines could hold
   different contents under one name. The fingerprint hash detects it after the fact — whether a set
   should also carry a declared version inside the folder is worth deciding once more than one set
   exists.
