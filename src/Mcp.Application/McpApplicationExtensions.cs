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
        services.TryAddSingleton<ToolCatalog>();
        return services;
    }
}
