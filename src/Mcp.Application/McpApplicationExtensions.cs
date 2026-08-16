using Mcp.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Mcp.Application;

/// <summary>Registration for the tool-hosting core. Providers register themselves separately — this
/// module never names one.</summary>
public static class McpApplicationExtensions
{
    public static IServiceCollection AddMcpApplication(this IServiceCollection services)
    {
        // TryAdd so a host that ships a real telemetry sink keeps it; the null sink is only a floor.
        services.TryAddSingleton<IUsageSink, NullUsageSink>();
        services.TryAddSingleton<AmbientCallerContext>();
        services.TryAddSingleton<ICallerContext>(sp => sp.GetRequiredService<AmbientCallerContext>());
        services.TryAddSingleton(TimeProvider.System);

        // TryAdd again: a host whose providers have legitimately slow calls registers its own
        // ToolCatalogOptions BEFORE this and keeps it. The default ceiling is what a host that says
        // nothing gets — never no ceiling at all.
        services.TryAddSingleton(new ToolCatalogOptions());
        services.TryAddSingleton<ToolCatalog>();
        return services;
    }
}
