# Third-party notices

This product is **sold**, and the server is **distributed** to customer machines. A licence position is
therefore a shipping fact, not a footnote — every entry below names the exact version whose licence was
resolved, and how.

The rule that governs additions and bumps: resolve the licence of the **exact version** from the artefact
itself, never from memory or from a summary someone wrote earlier. Metadata lies, and a licence can change
between versions of one package — FluentAssertions was Apache-2.0 through 7.x and moved to a non-commercial
licence in 8.0. **This rule earned its keep the day this file was written**: `dew_flow_rag_qln`'s notices
recorded `ModelContextProtocol` as MIT, and the 2.2.0 artefact says Apache-2.0. That error ran in the safe
direction for the reader and the unsafe one for us — Apache-2.0 attaches an attribution duty on
distribution that MIT does not. Corrected in both repositories.

## The position, in one paragraph

Every dependency is **MIT or Apache-2.0**. There is no copyleft component, nothing with a field-of-use
restriction, and nothing an operator installs separately. The only obligation that survives distribution is
Apache-2.0 attribution, which [NOTICE](NOTICE) satisfies and which must travel with any build.

| Scope | Packages | MIT | Apache-2.0 | Anything else |
|---|---|---|---|---|
| **Shipped** — resolved for `src/**` | 54 | 42 | 12 | none |
| **Build/test only** — resolved for `tests/**`, not distributed | 24 | 14 | 10 | none |

61 distinct package ids; the counts above exceed that because a few resolve at two versions across
projects, and both are listed.

## How this was resolved

```bash
dotnet restore dew_flow_mcp.slnx
# every package the projects actually resolve, per obj/project.assets.json ("type": "package"),
# then the <license> element of each package's own .nuspec in the global packages folder
```

Read from `obj/project.assets.json` rather than from `Directory.Packages.props`, deliberately: the props
file lists what we ASK for — 11 entries — and the assets file records what the restore actually produced,
transitive dependencies included: 78 resolutions. Reading the props file would have under-reported the
dependency set by a factor of seven.

Two things this method does not do, said rather than implied:

- It reads the `<license>` **expression** from the nuspec. For packages that instead embed a licence file,
  the file is what governs; none of the 61 here do — every one carries an SPDX expression.
- It resolves the licence of the package, not of everything vendored inside it. No package here vendors
  third-party source; if one ever does, this note is where that stops being true.

## Packages worth naming

| Package | Version | Licence | Note |
|---|---|---|---|
| `ModelContextProtocol` | 2.2.0 | **Apache-2.0** | The protocol SDK this server is built on, and the entry a sibling repository had recorded as MIT. Verified from `modelcontextprotocol.2.2.0.nuspec`: `<license type="expression">Apache-2.0`, repository `github.com/modelcontextprotocol/csharp-sdk`. Ships, so its attribution is in NOTICE. |
| `ModelContextProtocol.Core` | 2.2.0 | Apache-2.0 | Same SDK, same verification. |
| `ModelContextProtocol.AspNetCore` | 2.2.0 | Apache-2.0 | Same SDK. Only the HTTP transport needs it. |
| `Serilog` and its sinks | 4.3.0 / 10.0.0 / 6.1.1 / 7.0.0 / 3.0.0 | Apache-2.0 | Nine packages, the whole logging stack. Ships, and is the other half of what NOTICE exists for. |
| `FluentAssertions` | 7.2.2 | Apache-2.0 | **Do not bump.** 8.x moved to the Xceed Community License, which forbids commercial use without a paid subscription. 7.2.2 is the last Apache-2.0 release, and `Directory.Packages.props` carries the same warning at the pin. Test-only either way. |
| `xunit.v3` | 3.2.2 | Apache-2.0 | Test-only, never shipped. |
| `System.Drawing.Common` | 6.0.0 | MIT | Arrives transitively through the test platform's telemetry package. Not referenced by any `src/` project — worth naming only because a graphics dependency in a headless server looks like a mistake and is not one. |

## What is NOT here

- **No container images.** This server is a .NET process; it ships as a build output, not an image.
- **No GPU or vendor runtimes.** Those belong to the sidecar in `dew_flow_sidecar_rust`, which carries its
  own notices — DirectML and NVIDIA CUDA/cuDNN are vendor-licensed and never redistributed.
- **No retrieval stack.** This repository does not know that RAG exists (see [README.md](README.md)), so
  none of Qdrant, Postgres or Neo4j appears in its dependency graph. Neo4j Community in particular is
  GPL-3.0 and is the family's one hard line; it cannot reach this repository by construction.

## Appendix A — shipped packages (54)

| Package | Version | Licence |
|---|---|---|
| `Microsoft.AspNetCore.Authorization` | 10.0.11 | MIT |
| `Microsoft.AspNetCore.Components` | 10.0.11 | MIT |
| `Microsoft.AspNetCore.Components.Analyzers` | 10.0.11 | MIT |
| `Microsoft.AspNetCore.Components.Forms` | 10.0.11 | MIT |
| `Microsoft.AspNetCore.Components.Web` | 10.0.11 | MIT |
| `Microsoft.AspNetCore.Metadata` | 10.0.11 | MIT |
| `Microsoft.Extensions.AI.Abstractions` | 10.8.3 | MIT |
| `Microsoft.Extensions.Caching.Abstractions` | 10.0.10 | MIT |
| `Microsoft.Extensions.Configuration` | 10.0.0 | MIT |
| `Microsoft.Extensions.Configuration` | 10.0.11 | MIT |
| `Microsoft.Extensions.Configuration.Abstractions` | 10.0.0 | MIT |
| `Microsoft.Extensions.Configuration.Abstractions` | 10.0.10 | MIT |
| `Microsoft.Extensions.Configuration.Abstractions` | 10.0.11 | MIT |
| `Microsoft.Extensions.Configuration.Binder` | 10.0.0 | MIT |
| `Microsoft.Extensions.Configuration.Binder` | 10.0.11 | MIT |
| `Microsoft.Extensions.DependencyInjection` | 10.0.0 | MIT |
| `Microsoft.Extensions.DependencyInjection` | 10.0.11 | MIT |
| `Microsoft.Extensions.DependencyInjection.Abstractions` | 10.0.0 | MIT |
| `Microsoft.Extensions.DependencyInjection.Abstractions` | 10.0.11 | MIT |
| `Microsoft.Extensions.DependencyModel` | 10.0.0 | MIT |
| `Microsoft.Extensions.Diagnostics` | 10.0.11 | MIT |
| `Microsoft.Extensions.Diagnostics.Abstractions` | 10.0.0 | MIT |
| `Microsoft.Extensions.Diagnostics.Abstractions` | 10.0.10 | MIT |
| `Microsoft.Extensions.Diagnostics.Abstractions` | 10.0.11 | MIT |
| `Microsoft.Extensions.FileProviders.Abstractions` | 10.0.0 | MIT |
| `Microsoft.Extensions.FileProviders.Abstractions` | 10.0.10 | MIT |
| `Microsoft.Extensions.FileProviders.Abstractions` | 10.0.11 | MIT |
| `Microsoft.Extensions.Hosting.Abstractions` | 10.0.0 | MIT |
| `Microsoft.Extensions.Hosting.Abstractions` | 10.0.10 | MIT |
| `Microsoft.Extensions.Hosting.Abstractions` | 10.0.11 | MIT |
| `Microsoft.Extensions.Logging` | 10.0.0 | MIT |
| `Microsoft.Extensions.Logging.Abstractions` | 10.0.0 | MIT |
| `Microsoft.Extensions.Logging.Abstractions` | 10.0.11 | MIT |
| `Microsoft.Extensions.Options` | 10.0.0 | MIT |
| `Microsoft.Extensions.Options` | 10.0.10 | MIT |
| `Microsoft.Extensions.Options` | 10.0.11 | MIT |
| `Microsoft.Extensions.Options.ConfigurationExtensions` | 10.0.11 | MIT |
| `Microsoft.Extensions.Primitives` | 10.0.0 | MIT |
| `Microsoft.Extensions.Primitives` | 10.0.10 | MIT |
| `Microsoft.Extensions.Primitives` | 10.0.11 | MIT |
| `Microsoft.Extensions.Validation` | 10.0.11 | MIT |
| `Microsoft.JSInterop` | 10.0.11 | MIT |
| `ModelContextProtocol` | 2.2.0 | Apache-2.0 |
| `ModelContextProtocol.AspNetCore` | 2.2.0 | Apache-2.0 |
| `ModelContextProtocol.Core` | 2.2.0 | Apache-2.0 |
| `Serilog` | 4.3.0 | Apache-2.0 |
| `Serilog.AspNetCore` | 10.0.0 | Apache-2.0 |
| `Serilog.Extensions.Hosting` | 10.0.0 | Apache-2.0 |
| `Serilog.Extensions.Logging` | 10.0.0 | Apache-2.0 |
| `Serilog.Formatting.Compact` | 3.0.0 | Apache-2.0 |
| `Serilog.Settings.Configuration` | 10.0.0 | Apache-2.0 |
| `Serilog.Sinks.Console` | 6.1.1 | Apache-2.0 |
| `Serilog.Sinks.Debug` | 3.0.0 | Apache-2.0 |
| `Serilog.Sinks.File` | 7.0.0 | Apache-2.0 |
## Appendix B — build and test only (24)

Not distributed. Listed because "used" and "shipped" is a boundary this project records rather than assumes.

| Package | Version | Licence |
|---|---|---|
| `FluentAssertions` | 7.2.2 | Apache-2.0 |
| `Microsoft.ApplicationInsights` | 2.23.0 | MIT |
| `Microsoft.Bcl.AsyncInterfaces` | 6.0.0 | MIT |
| `Microsoft.Extensions.TimeProvider.Testing` | 10.0.0 | MIT |
| `Microsoft.Testing.Extensions.Telemetry` | 1.9.1 | MIT |
| `Microsoft.Testing.Extensions.TrxReport.Abstractions` | 1.9.1 | MIT |
| `Microsoft.Testing.Platform` | 1.9.1 | MIT |
| `Microsoft.Testing.Platform.MSBuild` | 1.9.1 | MIT |
| `Microsoft.Win32.Registry` | 5.0.0 | MIT |
| `Microsoft.Win32.SystemEvents` | 6.0.0 | MIT |
| `System.Configuration.ConfigurationManager` | 6.0.0 | MIT |
| `System.Drawing.Common` | 6.0.0 | MIT |
| `System.Security.Cryptography.ProtectedData` | 6.0.0 | MIT |
| `System.Security.Permissions` | 6.0.0 | MIT |
| `System.Windows.Extensions` | 6.0.0 | MIT |
| `xunit.analyzers` | 1.27.0 | Apache-2.0 |
| `xunit.v3` | 3.2.2 | Apache-2.0 |
| `xunit.v3.assert` | 3.2.2 | Apache-2.0 |
| `xunit.v3.common` | 3.2.2 | Apache-2.0 |
| `xunit.v3.core.mtp-v1` | 3.2.2 | Apache-2.0 |
| `xunit.v3.extensibility.core` | 3.2.2 | Apache-2.0 |
| `xunit.v3.mtp-v1` | 3.2.2 | Apache-2.0 |
| `xunit.v3.runner.common` | 3.2.2 | Apache-2.0 |
| `xunit.v3.runner.inproc.console` | 3.2.2 | Apache-2.0 |
