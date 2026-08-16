# Mcp.Ui — the console slice

A Razor **class library**, WASM-compatible. It has no host of its own and never will: it is mounted by a
Blazor SSR host that runs its interactivity in WebAssembly.

## Who mounts it

`Mcp.Host` does **not**, and that is not an oversight — that host is the standalone MCP server (clone,
run, and a CLI has workspace tools), and giving it a Blazor console would make the "no product needed"
bar heavier for a screen no CLI user opens.

The mount point is the product console in **`dew_flow_rag_qln`**, which vendors this repository as a
submodule at `external/dew_flow_mcp`:

| Where | What it does |
|---|---|
| `hosts/Daemon.Client/Daemon.Client.csproj` | references this project |
| `hosts/Daemon.Client/Routes.razor` | lists this assembly in `AdditionalAssemblies`, so the router finds `/mcp` |
| `hosts/Daemon.Client/Layout/NavMenu.razor` | the **MCP** nav entry |
| `hosts/Daemon.Client/Program.cs` and `hosts/Daemon/Program.cs` | `AddMcpUi()` — **both** hosts, see below |
| `hosts/AppHost/AppHost.cs` | the named `MCP` URL on the daemon resource, so the dashboard links straight to it |

Until 2026-08-16 this project held nothing but `_Imports.razor` — built, shipped, and wired to nothing.
The 24/7 audit called that out for a reason worth keeping written down: **a live-looking surface that is
not one is a trap for the next reader, and this repository is public.**

## Pages

| Route | What it answers |
|---|---|
| `/mcp` | What this server is *actually* advertising — every tool, the exact description text served, the schema hashes, the surface hashes, the build, and the health of the components behind it |

## Rules it follows

- **`.razor` for markup, `.razor.cs` for logic**, with primary-constructor injection — never `[Inject]`.
- **The `HttpClient` is the host's to register**, and `AddMcpUi()` deliberately does not build one. The
  address is not a single value: a WebAssembly client takes it once from `HostEnvironment.BaseAddress`,
  while a server-rendered host must take it **per request** from the incoming scheme and host, because
  SSR calls back into the same process. A fixed `Uri` cannot express the second, and a console guessing
  `localhost` reads a different server's surface than the one on screen.
  **Register it in BOTH hosts.** Nothing fails at startup when one forgets: every page that injects the
  service answers **500** on first render, and it reads as "one page is broken" rather than "the console
  is". The sibling slice in the product console was bitten by exactly that.
- **Absent is not empty.** Every read is a `Read<T>` carrying the value, whether it arrived, and why it
  did not. A daemon that cannot be reached and a server advertising no tools are opposite facts, and the
  page renders them as two different messages — the client half is pinned by `McpConsoleApiTests`.
- **The description is rendered in full**, not as a hash. A hash answers "did it change" and never "to
  what", and the wording is the thing an agent routes on.

## What is not here

No component tests. This repository has no bUnit harness, and adding one is its own decision rather than
something to fold into a page; the logic that *is* testable without one — the console's read side — is
tested in `tests/Mcp.Tests/McpConsoleApiTests.cs`. The markup is not covered, and this line exists so
that is a stated gap rather than an assumed one.
