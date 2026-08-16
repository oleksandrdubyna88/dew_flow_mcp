using Mcp.Ui.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Mcp.Ui;

/// <summary>Registration for the console slice. One call, so a host mounting these pages never learns
/// which services are behind them.</summary>
public static class McpUiExtensions
{
    /// <summary>Registers the console's read side.
    ///
    /// <para><b>The <see cref="HttpClient"/> is the HOST's to register, and deliberately not built here.
    /// </b> The address is not one value: a WebAssembly client takes it once from
    /// <c>HostEnvironment.BaseAddress</c>, while a server-rendered host must take it PER REQUEST from
    /// the incoming scheme and host, because server-side rendering calls back into the same process. A
    /// fixed <c>Uri</c> baked in here cannot express the second, and a console guessing <c>localhost</c>
    /// is how a page silently reads a different server's surface than the one on screen.</para>
    ///
    /// <para>This mirrors the sibling slice in the product console, which registers its own client the
    /// same way. The one scar worth knowing: when a host registers the API service but forgets the
    /// client, nothing fails at startup — every page that injects it answers <b>500</b> on first render,
    /// and the failure reads as "one page is broken" rather than "the console is".</para>
    /// </summary>
    public static IServiceCollection AddMcpUi(this IServiceCollection services)
    {
        services.TryAddScoped<McpConsoleApi>();
        return services;
    }
}
