# dew_flow_mcp

A Model Context Protocol server: one tool catalog, served over **stdio** and **HTTP/SSE**, plus a bridge
that hands the same tools to a local LLM in-process. Part of the DewFlow product.

> **The source is public; the licence is not open.** You may read this repository. Using it requires a
> commercial agreement — see [LICENSE](LICENSE). This is stated up front because a public repository with
> no such statement is usually read as an invitation.

## What this is not

**It does not know that retrieval exists.** No index, no embeddings, no vector store, no graph — none of it
appears in this repository's dependency graph, and that is enforced rather than remembered: an architecture
test in the consuming repository asserts that these assemblies carry no retrieval types, checked from both
sides of the boundary.

The mechanism is [`IToolProvider`](src/Mcp.Contracts/IToolProvider.cs). It is *declared* here and
*implemented* elsewhere, so the arrow points inward — a host adds its own providers and this repository
needs no edit to serve them. That inversion is the reason it can be public at all.

Two more deliberate absences:

- **No editing tools.** The public surface reads. The write path stays in the private product, and that
  boundary is easier to hold now than to walk back later.
- **No console of its own.** [`Mcp.Ui`](src/Mcp.Ui/README.md) is a mountable Blazor slice, not a host. The
  standalone server stays a CLI process; giving it a web console would make the clone-and-run bar heavier
  for a screen no CLI user opens.

## Run it

```bash
dotnet run --project src/Mcp.Host                        # HTTP/SSE, the default
dotnet run --project src/Mcp.Host -- --stdio             # stdio, for a runtime that spawns it
dotnet run --project src/Mcp.Host -- --print-surface     # what this build advertises, as JSON, then exit
curl localhost:5000/api/mcp/health                       # component health
curl localhost:5000/api/mcp/surface                      # the same surface, from a running server
```

`--root <path>` chooses the workspace the tools may touch (default: the current directory).
`--spool <path>` turns on per-call telemetry; absent, nothing is written.

**stdio puts the JSON-RPC on stdout, so every log line goes to stderr.** That is enforced in the host, not
left to discipline — one log line on that stream corrupts the protocol and the failure looks like a
protocol bug rather than a logging one. `--print-surface` keeps the same contract: the JSON is the only
thing on stdout.

## The thing this repository actually sells

Not the transport. **The tool descriptions.** An agent picks a tool from its name, its description and its
schema, and gets exactly one chance; a well-implemented tool with a vague description is a tool nobody
calls correctly. Two measurements from the previous generation are why wording sits on the same footing as
code here:

- Given the same tasks *without* these tools, two different models opened every code task with
  identifier-alternation grep and searched in natural language **not once**. On this surface the same two
  models wrote full behavioural sentences — and that arm produced the series' only perfect score.
- A parameter documented as a payload knob measured as a **recall** setting: dropping a result limit from
  20 to 5 lost **29 %** of the ground truth while `r@1` and `r@3` stayed identical. The cost is invisible
  in any single call and shows up only across a set.

So a description is a measured artefact, and the surface is **configuration** rather than a compiled
constant — a description compiled into the binary can only be A/B-ed by rebuilding it:

```bash
--tools rt_read_local_file,rt_grep     # serve only these; absent, every registered tool is advertised
--descriptions <dir>                   # read descriptions from <dir>/<set>/<tool-name>.md
--description-set <name>               # which set; absent, <dir> itself is read
```

A missing or blank file falls back to the literal compiled into the provider, which is never empty.

## How it is put together

| Project | Role |
|---|---|
| `Mcp.Contracts` | The shapes everything shares: `IToolProvider`, `ToolSchema`, `ToolResult`, `IUsageSink`. References nothing. |
| `Mcp.Application` | `ToolCatalog` — the single dispatch point, with the per-call ceiling and the surface decorator. |
| `Mcp.Server` | `CatalogToolFunction` — **one** adapter from the catalog to the protocol. |
| `Mcp.Bridge` | The same catalog presented to a local LLM in-process. |
| `Mcp.Api` | `/api/mcp/health` and `/api/mcp/surface`. |
| `Mcp.Telemetry` | The usage spool, emitting the benchmark-owned `telemetry/v0` schema. |
| `Mcp.Ui` | A mountable Blazor slice showing the live surface. Not a host. |
| `Workspace.*` | The `rt_` tools: the real filesystem, sandboxed to `--root`. |
| `ServiceDefaults` | Logging — coloured to the console, a file per run segmented at UTC midnight. |

**One adapter, not three.** The SDK's attributed-method-per-tool route would have meant a second
hand-written surface and the bridge a third; `SurfaceParityTests` asserts that the protocol and the bridge
advertise identical names and byte-identical schemas, so parity holds by construction rather than by
review.

A duplicate tool name throws at startup, naming both providers, rather than silently shadowing one.

## Build and test

```bash
dotnet build dew_flow_mcp.slnx
./tests/Mcp.Tests/bin/Debug/net10.0/Mcp.Tests.exe
```

Tests are **xUnit v3 on Microsoft Testing Platform**: there is no VSTest testhost, so `dotnet test` aborts
with a `testhost.deps.json` error — a tooling mismatch, not a test failure. Run the executable. Filters are
MTP syntax: `--filter-class`, `--filter-method`, `--filter-namespace`.

## Documentation

- [research/architecture.md](research/architecture.md) — the system as it is, with diagrams
- [research/](research/) — one `module_*.md` per module, plus the design records of shipped plans
- [todo/](todo/) — plans for work not yet done
- [.claude/rules/](.claude/rules/) — the conventions every change follows

## Legal

- [LICENSE](LICENSE) — proprietary; public visibility grants no right of use
- [NOTICE](NOTICE) — third-party attribution that must travel with any build
- [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) — every dependency, its exact version, and how its
  licence was resolved
- [VERSIONING.md](VERSIONING.md) — what a version number promises about a tool schema
- [SECURITY.md](SECURITY.md) · [CONTRIBUTING.md](CONTRIBUTING.md)
