using Mcp.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Mcp.Bridge;

/// <summary>Who the bridge reports as the caller. The host names the model it drives; an unnamed one
/// is recorded as not captured with that as the reason, never as a plausible default.</summary>
public sealed record BridgeCaller(CallerIdentity Identity)
{
    private const string NoModel =
        "the host did not name the model driving the bridge";

    public static BridgeCaller Driving(string model) =>
        new(new CallerIdentity(
            Captured.Text("local-llm-bridge"),
            Captured.Unavailable("an in-process presentation carries no client version"),
            string.IsNullOrWhiteSpace(model) ? Captured.Unavailable(NoModel) : Captured.Text(model),
            "in-process"));
}

/// <summary>Registration for the local-LLM presentation.</summary>
public static class McpBridgeExtensions
{
    /// <param name="model">The model this host drives through the bridge. Empty is honest and
    /// supported — it records as not captured rather than guessing.</param>
    public static IServiceCollection AddLocalLlmToolBridge(this IServiceCollection services, string model = "")
    {
        services.TryAddSingleton(BridgeCaller.Driving(model));
        services.TryAddSingleton<LocalLlmToolBridge>();
        return services;
    }
}
