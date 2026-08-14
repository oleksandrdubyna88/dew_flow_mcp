using System.Collections.Frozen;
using System.Diagnostics;
using Mcp.Contracts;
using Microsoft.Extensions.Logging;

namespace Mcp.Application;

/// <summary>The one place tools are resolved and dispatched. Every presentation — the MCP protocol
/// server, the local-LLM bridge, the console — goes through THIS object, which is what makes a second
/// implementation of a tool structurally impossible rather than merely discouraged.</summary>
public sealed class ToolCatalog
{
    private readonly FrozenDictionary<string, IToolProvider> byName;
    private readonly IUsageSink usage;
    private readonly ILogger<ToolCatalog> logger;

    public ToolCatalog(IEnumerable<IToolProvider> providers, IUsageSink usage, ILogger<ToolCatalog> logger)
    {
        this.usage = usage;
        this.logger = logger;

        var all = providers.SelectMany(p => p.Tools.Select(t => (Schema: t, Provider: p))).ToList();
        GuardAgainstDuplicateNames(all);

        Advertised = [.. all.Select(x => x.Schema).OrderBy(t => t.Name, StringComparer.Ordinal)];
        byName = all.ToFrozenDictionary(x => x.Schema.Name, x => x.Provider, StringComparer.Ordinal);

        logger.LogInformation("Tool catalog built with {ToolCount} tool(s): {ToolNames}",
            Advertised.Count, string.Join(", ", Advertised.Select(t => t.Name)));
    }

    /// <summary>Every advertised tool, name-ordered so the surface is stable across restarts.</summary>
    public IReadOnlyList<ToolSchema> Advertised { get; }

    public async Task<ToolResult> InvokeAsync(ToolCall call, CancellationToken cancellationToken)
    {
        if (!byName.TryGetValue(call.Name, out var provider))
        {
            logger.LogWarning("Unknown tool {ToolName} requested", call.Name);
            return ToolResult.Failure($"Unknown tool '{call.Name}'.");
        }

        var started = Stopwatch.GetTimestamp();
        var result = await provider.InvokeAsync(call, cancellationToken);
        await RecordAsync(call.Name, started, result, cancellationToken);
        return result;
    }

    private async Task RecordAsync(string toolName, long started, ToolResult result, CancellationToken cancellationToken)
    {
        var elapsed = Stopwatch.GetElapsedTime(started);
        var failed = result is ToolResult.Failed;
        var chars = result.Match(ok => ok.Length, failed => failed.Length);
        await usage.RecordAsync(new ToolUsage(toolName, elapsed, failed, chars), cancellationToken);
    }

    /// <summary>Two providers claiming one name must stop the host, naming both. Silent shadowing is
    /// how a surface starts lying about what it runs.</summary>
    private static void GuardAgainstDuplicateNames(IReadOnlyList<(ToolSchema Schema, IToolProvider Provider)> all)
    {
        var duplicates = all
            .GroupBy(x => x.Schema.Name, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => $"'{g.Key}' claimed by {string.Join(" and ", g.Select(x => x.Provider.GetType().Name))}")
            .ToList();

        if (duplicates.Count > 0)
        {
            throw new InvalidOperationException(
                $"Duplicate tool name(s): {string.Join("; ", duplicates)}.");
        }
    }
}
