using Mcp.Application;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;

namespace Mcp.Server;

/// <summary>Publishes every catalog tool on the protocol surface. The server learns the tool list at
/// startup from the catalog — it never names a tool, so a new provider appears here for free.</summary>
public static class CatalogToolRegistration
{
    public static IMcpServerBuilder WithCatalogTools(this IMcpServerBuilder builder)
    {
        builder.Services.AddSingleton<IEnumerable<McpServerTool>>(sp =>
        {
            var catalog = sp.GetRequiredService<ToolCatalog>();
            return [.. catalog.Advertised.Select(schema =>
                McpServerTool.Create(new CatalogToolFunction(schema, catalog)))];
        });

        return builder;
    }
}
