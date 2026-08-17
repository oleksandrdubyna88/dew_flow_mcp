# PLAN — the MCP surface: from one proving tool to the product's public face

> Status: **open. Phase 3 shipped 2026-08-17; Phases 1 and 2 are the work.** The inversion and both
> transports are built and parity-tested, and the tool set is still one placeholder. Scope: `src/Mcp.*`,
> `src/Workspace.*`, and this repository's public presentation.
>
> **Phase 3 is done** — LICENSE, NOTICE, THIRD-PARTY-NOTICES.md, README.md, VERSIONING.md, CONTRIBUTING.md
> and SECURITY.md all exist, and `Directory.Build.props` carries an explicit `<Version>`. It was taken
> first because it was the only phase whose absence was doing damage: the repository is already public, and
> a public repository with no LICENSE is "all rights reserved" by silence — the source was readable and
> legally unusable by anyone, which is the opposite of why it is public.
>
> **Of what remains, the `rt_` family is the part that is not blocked.** It touches the filesystem and git
> and needs no index. The `rag_` and `graf_` families are gated on `dew_flow_rag_qln` supplying
> `IToolProvider` implementations, which it does not yet: its `hosts/Daemon/Program.cs` says so in its own
> comment, and the daemon serves exactly one MCP tool today, this repository's `rt_read_local_file`.
>
> Related: the RAG repo's `todo/PLAN_rag_product.md` (what supplies the retrieval tools) and
> `todo/PLAN_experiment_matrix.md` (how their behaviour is judged).

## Where this stands

The hard architectural part is done and guarded:

- [`IToolProvider`](../src/Mcp.Contracts/IToolProvider.cs) is declared here and implemented elsewhere. The
  arrow points **RAG → MCP**, never back, so this repository can be public without knowing that retrieval
  exists.
- [`ToolCatalog`](../src/Mcp.Application/ToolCatalog.cs) is the single dispatch point; a duplicate tool name
  throws naming both providers rather than silently shadowing one.
- [`CatalogToolFunction`](../src/Mcp.Server/CatalogToolFunction.cs) is **one** adapter for every tool. The
  SDK's attributed-method-per-tool route would have meant a second hand-written surface, and the bridge a
  third; `SurfaceParityTests` asserts the protocol and the local-LLM bridge advertise identical names and
  byte-identical schemas.
- Both transports work, and stdio's constraint is enforced rather than remembered: stdout carries the
  JSON-RPC, so logs go to stderr.

**And there is one real tool** — a workspace file read
([WorkspaceToolProvider.cs:14](../src/Workspace.Application/WorkspaceToolProvider.cs)) — deliberately, because
the tool set was always going to change completely. That change is this plan.

## The thing this repository actually sells

Not the transport. **The tool descriptions.** An agent picks a tool from its name, its description and its
schema, and gets exactly one chance; a well-implemented tool with a vague description is a tool nobody calls
correctly. The previous generation measured this and the result is the reason this plan puts wording on the
same footing as code:

- Given the same tasks *without* these tools, two different models opened every code task with
  identifier-alternation grep and searched in natural language **not once**. On this surface, the same two
  models wrote full behavioural sentences — and that arm produced the series' only perfect score.
- A parameter documented as a payload knob was measured as a **recall** setting: dropping a result limit
  from 20 to 5 lost **29 %** of the ground truth, while `r@1` and `r@3` stayed identical — so the cost is
  invisible in any single call and shows up only across a set.

The lesson is not "write nicer docs". It is that **a tool's description is a measured artefact**, and a
change to it is an arm of the experiment matrix — `dew_flow_rag_qln · todo/PLAN_experiment_matrix.md` —
like any retrieval change.

## Build order

### Phase 1 — the tool set, in three families

Named by what they touch, so an agent can route before reading:

| prefix | touches | examples |
|---|---|---|
| `rt_` | the real filesystem and git, now | read a file or a window of it, glob, literal scan, regex, list a directory, git status/diff |
| `rag_` | the semantic index | search code by behaviour, search docs by meaning, fetch one member's source, outline a file |
| `graf_` | the dependency graph | find a symbol by name or signature fragment, one member's callers and callees, the whole graph |

Two rules that are not negotiable, both learned the expensive way:

1. **A read must be able to return a line WINDOW.** A tool that only returns whole files forces an agent to
   pull megabytes to see forty lines, and the agent will do it every time.
2. **Every response says how fresh it is.** A hit whose file has uncommitted changes is flagged, and
   "could not sample" is reported as unknown rather than as clean — absence of a flag must never read as
   evidence.

### Phase 2 — what a public server owes its callers

- **Authentication on the HTTP transport.** Today it is open. Fine on localhost; not fine the first time
  someone binds it to a LAN, and someone will.
- **A refusal is a refusal.** Already pinned by `ProtocolErrorFlagTests` after a live finding: a refused call
  reached the wire as ordinary content with no `isError`, so the model read the refusal as an answer.
- ~~**Usage metering.**~~ **Shipped 2026-08-15** — see
  [research/PLAN_usage_telemetry.md](../research/PLAN_usage_telemetry.md).
  [`IUsageSink`](../src/Mcp.Contracts/IUsageSink.cs) now has a real implementation (a local spool
  emitting the benchmark-owned `telemetry/v0` schema), every call records its caller, its scope, its
  byte-budgeted arguments and its three-state outcome, and `NullUsageSink` remains the default so a
  host opts in. Open tail there: the private product host's registration line, and the ingest side.
- **Cancellation that reaches the work**, not just the request.

### Phase 3 — being a public repository — **DONE 2026-08-17**

Taken first, out of the plan's own order, because it was the only phase already costing something: the
repository was public with no LICENSE, which is "all rights reserved" by silence — readable and legally
unusable, the opposite of the intent.

What shipped:

- **[LICENSE](../LICENSE)** — proprietary, source-available; the operator chose the family default that
  `ClaudeRag` set. Its section 0 exists only in this repository's copy and says the thing the file is for:
  *the source is public, that is not a licence*. `{{COPYRIGHT_HOLDER}}` and the counsel banner stay until a
  lawyer reviews it — that is deliberate, not unfinished.
- **[NOTICE](../NOTICE)** — the Apache-2.0 §4(d) attribution that must travel with any build, naming the
  Serilog stack and the MCP SDK.
- **[THIRD-PARTY-NOTICES.md](../THIRD-PARTY-NOTICES.md)** — 78 resolutions over 61 distinct packages: 56
  MIT, 20 Apache-2.0, no copyleft, nothing separately installed. Resolved from `obj/project.assets.json`
  rather than `Directory.Packages.props`, because the props file lists 11 things we ASK for and the assets
  file records what restore produced.
- **[README.md](../README.md)** — what this is, what it is not, and five lines to run both transports.
  Every claim in it was verified against a running server rather than read off the source.
- **[VERSIONING.md](../VERSIONING.md)** plus an explicit `<Version>` in `Directory.Build.props`.
- **[CONTRIBUTING.md](../CONTRIBUTING.md)** and **[SECURITY.md](../SECURITY.md)**, both short.

**The finding worth keeping.** The convention this phase was told to follow — *resolve the licence of the
exact version from the artefact, never from metadata* — caught a live error on its first application:
`dew_flow_rag_qln`'s notices recorded `ModelContextProtocol` as **MIT**, and the 2.2.0 nuspec says
**Apache-2.0**. It ran in the unsafe direction, because Apache-2.0 attaches an attribution duty on
distribution that MIT does not. Corrected in both repositories. The rule was written after a package that
declared one licence and classified itself as another; this time it caught a human summary instead, which
is the more ordinary failure.

**Two deviations from the phase as written.** It asked for a versioning *story* and got a document with a
table of what breaks a tool surface, because the interesting breaking changes here are the ones that leave
every call compiling — a changed parameter MEANING, a changed default, a description rewritten to describe
different behaviour. And it did not ask for `<Version>` to be set: the surface fingerprint reports the
version to callers, so the SDK's silent `1.0.0` was already a promise nobody had made.

### Phase 4 — what stays out, and why that is a decision

**Editing tools do not live here.** The public surface reads; the write path stays in the private product.
This is the boundary that lets the repository be public at all, and it is easier to hold now than to walk
back later.

## Definition of Done

- [ ] The three tool families are implemented, and every one of them reaches both the protocol and the
      bridge through the single adapter — asserted, not assumed.
- [ ] Every tool description states what the tool is FOR in behavioural terms, and every parameter whose
      effect is non-obvious says what it actually costs.
- [ ] Reads take a line window; responses carry freshness; refusals set `isError`.
- [ ] The HTTP transport authenticates.
- [x] `IUsageSink` has an implementation and per-call usage is recorded.
- [x] README, LICENSE, notices and a version policy exist before the repository is advertised.
      *(2026-08-17. One thing is deliberately left open inside them: `{{COPYRIGHT_HOLDER}}` is a placeholder
      and LICENSE carries an ACTION REQUIRED banner, because naming a legal entity and clearing the text is
      counsel's call, not this task's.)*
- [ ] No editing tool has appeared in this repository.
