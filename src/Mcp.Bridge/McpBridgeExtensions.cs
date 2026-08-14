using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Mcp.Bridge;

/// <summary>Registration for the local-LLM presentation.</summary>
public static class McpBridgeExtensions
{
    public static IServiceCollection AddLocalLlmToolBridge(this IServiceCollection services)
    {
        services.TryAddSingleton<LocalLlmToolBridge>();
        return services;
    }
}
