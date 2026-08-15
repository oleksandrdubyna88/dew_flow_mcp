using Mcp.Application;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;

namespace Mcp.Server;

/// <summary>Publishes every catalog tool on the protocol surface. The server learns the tool list at
/// startup from the catalog — it never names a tool, so a new provider appears here for free.</summary>
public static class CatalogToolRegistration
{
    /// <param name="transport">Which transport this server is published on — <c>stdio</c> or
    /// <c>http</c>. Recorded with every call, and passed in rather than inferred because the host is
    /// the only party that knows: it chose the transport one line earlier.</param>
    public static IMcpServerBuilder WithCatalogTools(this IMcpServerBuilder builder, string transport = "")
    {
        builder.Services.AddSingleton<IEnumerable<McpServerTool>>(sp =>
        {
            var catalog = sp.GetRequiredService<ToolCatalog>();
            return [.. catalog.Advertised.Select(schema =>
                McpServerTool.Create(new CatalogToolFunction(schema, catalog)))];
        });

        return builder.WithCallerContext(transport);
    }
}
