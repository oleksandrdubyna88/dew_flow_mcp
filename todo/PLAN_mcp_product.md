# PLAN — the MCP surface: from one proving tool to the product's public face

> Status: **plan; the inversion and both transports are built and parity-tested, the tool set is one
> placeholder.** Scope: `src/Mcp.*`, `src/Workspace.*`, and this repository's public presentation.
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
change to it is an arm of [the experiment matrix](../../dew_flow_rag_qln/todo/PLAN_experiment_matrix.md) like
any retrieval change.

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
- **Usage metering.** [`IUsageSink`](../src/Mcp.Contracts/IUsageSink.cs) is a port with no implementation.
  Per-call accounting is what makes cost visible before it is a surprise on a bill.
- **Cancellation that reaches the work**, not just the request.

### Phase 3 — being a public repository

This one is public, and nothing about that is currently true except the visibility flag. It needs, before
anyone is pointed at it:

- A README that says what this is, what it is *not* (it does not know about retrieval), and how to run both
  transports in five lines.
- A LICENSE, and `THIRD-PARTY-NOTICES.md` for the dependency set — the sibling repos already carry the
  convention: resolve the licence of the **exact version** from the artefact, never from metadata, because
  metadata lies and licences change between versions of one package.
- A versioning and release story. A tool schema is a contract; changing a parameter's meaning without a
  version is how a customer's agent starts calling the wrong thing quietly.
- Contribution and security notes, kept short.

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
- [ ] `IUsageSink` has an implementation and per-call usage is recorded.
- [ ] README, LICENSE, notices and a version policy exist before the repository is advertised.
- [ ] No editing tool has appeared in this repository.
