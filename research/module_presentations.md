# Module — presentations

> `src/Mcp.Server`, `src/Mcp.Bridge`. The system as it is, 2026-08-15.

## Purpose

Publish one catalog on two surfaces — the MCP protocol and in-process function calling — without either
one growing tool logic of its own, and establish **who is calling** while it does.

The second half is not bookkeeping. Server-side telemetry is worth having because it sees traffic no
harness does, and "which tool, how often" is a much weaker question than "by whom, against what". The
identity was always on the wire; this server simply never read it until 2026-08-15.

## Flow

```mermaid
sequenceDiagram
    participant C as MCP client
    participant O as McpServerOptions filters
    participant F as CallerContextFilter
    participant X as AmbientCallerContext
    participant A as CatalogToolFunction
    participant T as ToolCatalog

    C->>O: tools/call
    O->>F: CallToolFilters pipeline
    F->>F: request.Server.ClientInfo
    Note over F: name and version are on the wire;<br/>the MODEL is not, in any revision
    F->>X: Enter(CallerIdentity)
    F->>A: next(request)
    A->>T: InvokeAsync(ToolCall)
    T->>X: Current (read while assembling ToolUsage)
    T-->>A: ToolResult
    A-->>C: CallToolResult, isError for Refused and Failed
    F->>X: dispose — restore the previous caller
```

The bridge does the same in one step: it enters its own identity, calls the catalog, and disposes.

## Core types

| Type | Role |
|---|---|
| `CatalogToolFunction` | **One** `AIFunction` adapter for every tool. The SDK's usual route is an attributed method per tool, which would mean writing each tool twice — once for the protocol and once for the bridge |
| `CatalogToolRegistration.WithCatalogTools(transport)` | Publishes every advertised tool and installs the caller filter. `transport` is passed in because the host is the only party that knows which one it chose |
| `CallerContextFilter` | A `CallToolFilters` entry that reads `ClientInfo` and scopes the identity |
| `AmbientCallerContext` | `AsyncLocal` holder. One process serves many concurrent sessions; a single mutable slot would attribute whichever call finished last to whoever asked first |
| `LocalLlmToolBridge` | The in-process surface. `ToolDefinitions` is a projection of `ToolCatalog.Advertised`; both `InvokeAsync` overloads forward to the catalog |
| `BridgeCaller` | Who the bridge reports as the caller. `Driving(model)` — an unnamed model records as *not captured*, never as a plausible default |

## Entry points

| Member | Purpose |
|---|---|
| `IMcpServerBuilder.WithCatalogTools(string transport = "")` | The protocol surface |
| `IServiceCollection.AddLocalLlmToolBridge(string model = "")` | The in-process surface |
| `LocalLlmToolBridge.ToolDefinitions` | The catalog rendered as OpenAI-style function definitions |
| `LocalLlmToolBridge.InvokeAsync(name, JsonElement \| string, ct)` | Runs one call; the string overload turns malformed JSON into a tool failure the model can read and retry |

## Three rules that are asserted rather than remembered

- **Parity.** `SurfaceParityTests` fails the build if the two surfaces advertise different names or
  byte-different schemas, or if either grows a tool of its own. A new provider reaches both with no edit
  inside the MCP module.
- **A refusal reaches the wire flagged.** `ProtocolErrorFlagTests` pins that `Refused` and `Failed` both
  set `isError`. Found live: the first working server answered a sandbox denial as ordinary content, so
  a caller could not tell "refused" from "read an empty file".
- **And it reaches the BRIDGE just as distinguishably** (2026-08-19). The two rules above left a gap: they
  proved the surfaces advertise the same tools and run the same code, and that the *wire* flags an error —
  nothing checked how the in-process presentation signals one, which is the half where the original defect
  lived. `BridgeErrorParityTests` pins it: a refusal arrives as `ToolResult.Refused` and never as an `Ok`
  carrying the reason as content, both surfaces agree that something went wrong whichever case it was, and
  the text is the same on both.

  The two surfaces are deliberately **not** identical here, and the tests pin the asymmetry rather than
  ironing it out: the protocol has one error state, so `Refused` and `Failed` collapse into one flag on the
  wire — inventing a second would break every conforming client to express a difference only telemetry needs
  — while the bridge hands over the union and keeps all three. More information on one side is fine; an error
  arriving as a success on either side is not. Verified by breaking the bridge on purpose: collapsing its
  result to `Ok` turns 5 of the 6 tests red, and the one that stays green is the success case.

## What the transport can and cannot tell us

| Field | Source | Availability |
|---|---|---|
| client name, version | MCP `ClientInfo` on the request-scoped server | Captured for any conforming client |
| transport | passed in by the host at registration | Always |
| **model** | — | **Never, on the protocol surface.** No MCP revision carries it. Populated only by the bridge, when the host names the model it drives |

Deriving the model from the client name would be the single most tempting and most wrong field in the
record: an agent called `claude-code` may be running anything.

## Dependencies

- `Mcp.Server` → `Mcp.Application`, `Mcp.Contracts`, `ModelContextProtocol` 2.2.0.
- `Mcp.Bridge` → `Mcp.Application`, `Mcp.Contracts`. No protocol dependency at all.

## Tests

`SurfaceParityTests.cs`, `ProtocolErrorFlagTests.cs`, `BridgeErrorParityTests.cs`, `CallerIdentityTests.cs`,
`ProtocolCallerIdentityTests.cs`. The last runs a **real** `McpClient` against a **real** `McpServer`
over an in-memory stream pair — the claim is about what the transport carries, and a hand-made context
would prove only that the hand-made context has the field somebody put in it.

`FixedProvider` — a provider whose one tool always answers the same way — is shared by the two error tests
rather than copied into each: two fixed providers would be two things to keep in step with one three-case
union, which is how the third case comes to be tested on one surface and not the other.
