using Mcp.Application;
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
    /// <summary>The request-timeout policy this group runs under. The NAME lives here because the routes
    /// do; the VALUE lives in the host's <c>appsettings.json</c>, because an operator changing a timeout
    /// must not need a rebuild.
    /// <para>Scoped to this group deliberately, never applied as a global default: <c>MapMcp()</c> serves
    /// a Server-Sent Events stream meant to stay open, and a blanket request timeout would sever it on a
    /// schedule. These endpoints answer in milliseconds and have no long-lived shape.</para></summary>
    public const string TimeoutPolicy = "mcp-management-api";

    public static IEndpointRouteBuilder MapMcpApi(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/mcp");

        // Applied to the group, return discarded: WithRequestTimeout hands back an
        // IEndpointConventionBuilder, and chaining it would lose the RouteGroupBuilder the routes below
        // need. The convention reaches every endpoint in the group either way.
        group.WithRequestTimeout(TimeoutPolicy);

        // Health is COMPUTED, from whatever registered itself as a contributor. It used to be the
        // constant "ok", which meant an orchestrator polling it could not see the dead telemetry
        // writer behind it — the probe answered for the route, not for the server.
        group.MapGet("/health", ([FromServices] IEnumerable<IHealthContributor> components) =>
            TypedResults.Ok(McpApiHealth.From(components)));

        // Declare and echo, never assume: what a configuration ASKED for and what this process is
        // SERVING are two different facts, and only the second one explains a result. The hashes are
        // computed server-side and quoted — a consumer compares the string, never re-derives it.
        group.MapGet("/surface", ([FromServices] SurfaceFingerprintReader surface) =>
            TypedResults.Ok(surface.Read()));

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
