using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
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
        group.MapGet("/health", () => TypedResults.Ok(new McpApiHealth("ok")));
        return endpoints;
    }
}

/// <summary>Liveness answer for the management surface.</summary>
public record McpApiHealth(string Status);
