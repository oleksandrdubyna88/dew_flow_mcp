using Mcp.Api;
using Mcp.Diagnostics;
using Mcp.Application;
using Mcp.Bridge;
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
var workspaceRoot = ReadOption(args, "--root") ?? Directory.GetCurrentDirectory();
var spool = ReadOption(args, "--spool");

if (args.Contains("--stdio"))
{
    var stdio = Host.CreateApplicationBuilder(args);

    // stdio uses stdout for the protocol, so logs MUST go to stderr — never stdout. One log line on that
    // stream corrupts the JSON-RPC and looks like a protocol bug rather than a logging one.
    stdio.AddDewFlowLogging("mcp-stdio", consoleToStdErr: true);

    AddToolStack(stdio.Services, workspaceRoot, spool, "mcp-stdio");
    stdio.Services.AddMcpServer().WithStdioServerTransport().WithCatalogTools("stdio");

    await stdio.Build().RunAsync();
    return;
}

var builder = WebApplication.CreateBuilder(args);
builder.AddDewFlowLogging("mcp");

AddToolStack(builder.Services, workspaceRoot, spool, "mcp");
builder.Services.AddMcpServer().WithHttpTransport().WithCatalogTools("http");

var app = builder.Build();

app.MapMcp();
app.MapMcpApi();

app.Run();

static void AddToolStack(IServiceCollection services, string workspaceRoot, string? spool, string app)
{
    services.AddMcpApplication();          // the catalog + the null usage sink
    services.AddTelemetrySpool(spool, app); // replaces the null sink ONLY when a spool was named
    services.AddLocalLlmToolBridge();      // the in-process presentation, same catalog
    services.AddWorkspaceTools(workspaceRoot);
}

static string? ReadOption(string[] args, string name)
{
    var index = Array.IndexOf(args, name);
    return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
}
