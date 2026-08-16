using Mcp.Api;
using Mcp.Diagnostics;
using Mcp.Application;
using Mcp.Bridge;
using Mcp.Contracts;
using Mcp.Server;
using Mcp.Telemetry;
using Microsoft.Extensions.Logging;
using Workspace.Infrastructure;

// Two transports over ONE catalog:
//   • HTTP/SSE (default) — for CLI runtimes that connect over HTTP.
//   • stdio (--stdio)    — for runtimes that launch the server as a subprocess.
// The local-LLM bridge is a THIRD presentation over the same catalog, registered here so a host that
// drives a local model in-process needs no second implementation of anything.
//
// --root <path> chooses the workspace the tools may touch; it defaults to the current directory.
// --spool <path> turns on per-call telemetry; absent, the null sink stays and nothing is written.
//
// The tool surface itself is configuration, because a tool's DESCRIPTION is a measured artefact and one
// compiled into the binary can only be A/B-ed by rebuilding it:
//   --tools a,b,c            serve only these tools; absent, every registered tool is advertised.
//   --descriptions <dir>     read descriptions from <dir>/<set>/<tool-name>.md.
//   --description-set <name> which set; absent, <dir> itself is read.
// A missing or blank file falls back to the literal compiled into the provider, which is never empty.
var workspaceRoot = ReadOption(args, "--root") ?? Directory.GetCurrentDirectory();
var spool = ReadOption(args, "--spool");
var surface = ToolSurfaceOptions.From(
    ReadOption(args, "--tools") ?? string.Empty,
    ReadOption(args, "--descriptions") ?? string.Empty,
    ReadOption(args, "--description-set") ?? string.Empty);

var stdio = args.Contains("--stdio");
var printSurface = args.Contains("--print-surface");

// --correlation <leg[/phase]> stamps every telemetry record this PROCESS writes. Refused on the HTTP
// transport rather than quietly applied: one value across concurrent callers would invent an
// attribution, and an invented one is worse than none. The surface probe serves nobody, so it is not
// the shared case either way.
var correlation = TelemetryCorrelation.Declared(
    ReadOption(args, "--correlation") ?? string.Empty,
    sharedTransport: !stdio && !printSurface);

// --print-surface answers "what is this build actually advertising" and exits. It needs no port, so it
// works for the stdio shape too, and it is the natural instrument for a startup check or a CI
// assertion. The JSON is the ONLY thing on stdout — logs go to stderr, the same contract stdio keeps.
if (printSurface)
{
    var probe = Host.CreateApplicationBuilder(args);
    probe.AddDewFlowLogging("mcp-surface", consoleToStdErr: true);

    // No spool: a process that answers one question and exits must not open a telemetry writer.
    AddToolStack(probe.Services, workspaceRoot, spool: null, "mcp-surface", surface, correlation);

    using var probed = probe.Build();
    Console.Out.WriteLine(
        SurfaceJson.Text(probed.Services.GetRequiredService<SurfaceFingerprintReader>().Read()));
    return;
}

if (stdio)
{
    var stdioHost = Host.CreateApplicationBuilder(args);

    // stdio uses stdout for the protocol, so logs MUST go to stderr — never stdout. One log line on that
    // stream corrupts the JSON-RPC and looks like a protocol bug rather than a logging one.
    stdioHost.AddDewFlowLogging("mcp-stdio", consoleToStdErr: true);

    AddToolStack(stdioHost.Services, workspaceRoot, spool, "mcp-stdio", surface, correlation);
    stdioHost.Services.AddMcpServer().WithStdioServerTransport().WithCatalogTools("stdio");

    await stdioHost.Build().RunAsync();
    return;
}

var builder = WebApplication.CreateBuilder(args);
builder.AddDewFlowLogging("mcp");

AddToolStack(builder.Services, workspaceRoot, spool, "mcp", surface, correlation);
builder.Services.AddMcpServer().WithHttpTransport().WithCatalogTools("http");

var app = builder.Build();

app.MapMcp();
app.MapMcpApi();

app.Run();

static void AddToolStack(
    IServiceCollection services,
    string workspaceRoot,
    string? spool,
    string app,
    ToolSurfaceOptions surface,
    TelemetryCorrelation correlation)
{
    services.AddMcpApplication(surface);   // the catalog + the null usage sink
    services.AddSurfaceFingerprint(app);   // the echo: --print-surface and GET /api/mcp/surface
    services.AddTelemetrySpool(spool, app, correlation); // replaces the null sink ONLY with a spool
    services.AddLocalLlmToolBridge();      // the in-process presentation, same catalog
    services.AddWorkspaceTools(workspaceRoot);
}

static string? ReadOption(string[] args, string name)
{
    var index = Array.IndexOf(args, name);
    return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
}
