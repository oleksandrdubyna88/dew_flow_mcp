using Mcp.Ui.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Mcp.Ui;

/// <summary>Registration for the console slice. One call, so a host mounting these pages never learns
/// which services are behind them.</summary>
public static class McpUiExtensions
{
    /// <summary>Registers the console's read side against the daemon that serves the management API.
    /// <para>The base address is the HOST's to supply, and it is not defaulted: a WASM client and a
    /// server-rendered one reach the same API by different addresses, and a console guessing
    /// <c>localhost</c> is how a page silently reads a different server's surface than the one the
    /// operator is looking at.</para></summary>
    public static IServiceCollection AddMcpUi(this IServiceCollection services, Uri apiBaseAddress)
    {
        services.TryAddScoped(_ => new McpConsoleApi(new HttpClient { BaseAddress = apiBaseAddress }));
        return services;
    }
}
