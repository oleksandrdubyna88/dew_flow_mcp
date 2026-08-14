using Mcp.Api;
using Mcp.Application;
using Mcp.Bridge;
using Mcp.Server;
using Microsoft.Extensions.Logging;
using Workspace.Infrastructure;

// Two transports over ONE catalog:
//   • HTTP/SSE (default) — for CLI runtimes that connect over HTTP.
//   • stdio (--stdio)    — for runtimes that launch the server as a subprocess.
// The local-LLM bridge is a THIRD presentation over the same catalog, registered here so a host that
// drives a local model in-process needs no second implementation of anything.
//
// --root <path> chooses the workspace the tools may touch; it defaults to the current directory.
var workspaceRoot = ReadOption(args, "--root") ?? Directory.GetCurrentDirectory();

if (args.Contains("--stdio"))
{
    var stdio = Host.CreateApplicationBuilder(args);

    // stdio uses stdout for the protocol, so logs MUST go to stderr — never stdout.
    stdio.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

    AddToolStack(stdio.Services, workspaceRoot);
    stdio.Services.AddMcpServer().WithStdioServerTransport().WithCatalogTools();

    await stdio.Build().RunAsync();
    return;
}

var builder = WebApplication.CreateBuilder(args);

AddToolStack(builder.Services, workspaceRoot);
builder.Services.AddMcpServer().WithHttpTransport().WithCatalogTools();

var app = builder.Build();

app.MapMcp();
app.MapMcpApi();

app.Run();

static void AddToolStack(IServiceCollection services, string workspaceRoot)
{
    services.AddMcpApplication();          // the catalog + the null usage sink
    services.AddLocalLlmToolBridge();      // the in-process presentation, same catalog
    services.AddWorkspaceTools(workspaceRoot);
}

static string? ReadOption(string[] args, string name)
{
    var index = Array.IndexOf(args, name);
    return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
}
