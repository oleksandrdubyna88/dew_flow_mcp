using Mcp.Application;
using Mcp.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Mcp.Server;

/// <summary>Teaches the protocol surface who is calling.
/// <para>
/// The identity was always on the wire and this server simply never read it: the client declares its
/// name and version, and every tool call arrives with a <see cref="RequestContext{T}"/> whose
/// request-scoped server carries it. A call-tool filter is the documented seam that sees every such
/// request — including calls to tools this server does not advertise — which is exactly the set the
/// catalog meters.
/// </para>
/// <para>
/// The MODEL is not on the wire in any revision, so it is recorded as not captured with that as the
/// reason. Guessing it from the client name would be the single most tempting and most wrong field in
/// the whole record: "claude-code" says nothing about which model is driving the session.
/// </para></summary>
internal static class CallerContextFilter
{
    private const string NoModel =
        "the MCP protocol carries no model identity for the caller";

    private const string NoClientInfo =
        "the client sent no implementation info with this request";

    public static IMcpServerBuilder WithCallerContext(this IMcpServerBuilder builder, string transport)
    {
        builder.Services.Configure<McpServerOptions>(options =>
            options.Filters.Request.CallToolFilters.Add(next => async (request, cancellationToken) =>
            {
                var callers = request.Services?.GetService<AmbientCallerContext>();
                if (callers is null)
                {
                    return await next(request, cancellationToken);
                }

                using (callers.Enter(Identify(request, transport)))
                {
                    return await next(request, cancellationToken);
                }
            }));

        return builder;
    }

    private static CallerIdentity Identify(RequestContext<CallToolRequestParams> request, string transport)
    {
        var client = request.Server.ClientInfo;

        return new CallerIdentity(
            client is null ? Captured.Unavailable(NoClientInfo) : Captured.Text(client.Name),
            client is null ? Captured.Unavailable(NoClientInfo) : Captured.Text(client.Version ?? string.Empty),
            Captured.Unavailable(NoModel),
            transport);
    }
}
