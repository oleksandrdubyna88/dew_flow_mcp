using Mcp.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Mcp.Api;

/// <summary>Mounts the MCP management surface as one minimal-API route group.
/// Every future endpoint of this module hangs off the same group — the host calls
/// <see cref="MapMcpApi"/> once and never learns individual routes.</summary>
public static class McpApiEndpoints
{
    public static IEndpointRouteBuilder MapMcpApi(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/mcp");

        // Health is COMPUTED, from whatever registered itself as a contributor. It used to be the
        // constant "ok", which meant an orchestrator polling it could not see the dead telemetry
        // writer behind it — the probe answered for the route, not for the server.
        group.MapGet("/health", ([FromServices] IEnumerable<IHealthContributor> components) =>
            TypedResults.Ok(McpApiHealth.From(components)));

        return endpoints;
    }
}

/// <summary>Liveness answer for the management surface: one verdict, and the components it was
/// computed from.
/// <para>A degraded server still answers <b>200</b>, deliberately. The status code says the process is
/// serving; the body says how well. A 503 for a broken spool would tell a supervisor to restart a
/// server that is answering every tool call correctly — and the restart would lose more than the
/// telemetry did.</para></summary>
public sealed record McpApiHealth(string Status, IReadOnlyList<ComponentHealth> Components)
{
    public const string Ok = "ok";

    public const string Degraded = "degraded";

    /// <summary>Asks every contributor and folds the answers into one verdict. No component means
    /// nothing to report, not a claim that anything was checked — which is why the components travel
    /// with the status instead of being summarised away.</summary>
    public static McpApiHealth From(IEnumerable<IHealthContributor> contributors)
    {
        var components = contributors.Select(c => c.Check()).ToList();
        return new McpApiHealth(components.All(c => c.Healthy) ? Ok : Degraded, components);
    }
}
