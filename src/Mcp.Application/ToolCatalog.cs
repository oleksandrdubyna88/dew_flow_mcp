using System.Collections.Frozen;
using System.Diagnostics;
using Mcp.Contracts;
using Microsoft.Extensions.Logging;

namespace Mcp.Application;

/// <summary>The one place tools are resolved and dispatched. Every presentation — the MCP protocol
/// server, the local-LLM bridge, the console — goes through THIS object, which is what makes a second
/// implementation of a tool structurally impossible rather than merely discouraged.
/// <para>It is therefore also the one place that can see every call, which is why the telemetry record
/// is assembled here and nowhere else.</para></summary>
public sealed class ToolCatalog
{
    /// <summary>What one call may store of its arguments and of its answer. Deliberately modest: the
    /// point is to know WHAT was asked, not to keep a second copy of the repository.</summary>
    public const int DefaultPayloadBudgetBytes = 4096;

    private readonly FrozenDictionary<string, IToolProvider> byName;
    private readonly IUsageSink usage;
    private readonly ICallerContext callers;
    private readonly TimeProvider clock;
    private readonly ILogger<ToolCatalog> logger;

    public ToolCatalog(
        IEnumerable<IToolProvider> providers,
        IUsageSink usage,
        ICallerContext callers,
        TimeProvider clock,
        ILogger<ToolCatalog> logger)
    {
        this.usage = usage;
        this.callers = callers;
        this.clock = clock;
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
        var started = Stopwatch.GetTimestamp();

        if (!byName.TryGetValue(call.Name, out var provider))
        {
            logger.LogWarning("Unknown tool {ToolName} requested", call.Name);
            var unknown = ToolResult.Failure($"Unknown tool '{call.Name}'.");

            // Metered like any other call: a client repeatedly asking for a tool this server does not
            // advertise is a fact about the surface — usually a stale configuration — and dropping it
            // makes the one call worth investigating the only one nobody records.
            await RecordAsync(call, string.Empty, started, unknown, cancellationToken);
            return unknown;
        }

        var result = await provider.InvokeAsync(call, cancellationToken);
        await RecordAsync(call, provider.Scope, started, result, cancellationToken);
        return result;
    }

    private async Task RecordAsync(
        ToolCall call,
        string scope,
        long started,
        ToolResult result,
        CancellationToken cancellationToken)
    {
        var (arguments, argumentsDropped) =
            PayloadBudget.Apply(call.Arguments.GetRawText(), DefaultPayloadBudgetBytes);
        var text = result.Text;
        var (body, bodyDropped) = PayloadBudget.Apply(text, DefaultPayloadBudgetBytes);

        await usage.RecordAsync(
            new ToolUsage(
                call.Name,
                clock.GetUtcNow(),
                callers.Current,
                scope,
                arguments,
                argumentsDropped,
                Outcome(result),
                result is ToolResult.Ok ? string.Empty : text,
                text.Length,
                body,
                bodyDropped,
                CapturedCount.Unavailable("this surface does not count tokens"),
                Stopwatch.GetElapsedTime(started)),
            cancellationToken);
    }

    private static ToolOutcome Outcome(ToolResult result) =>
        result.Match(_ => ToolOutcome.Answered, _ => ToolOutcome.Refused, _ => ToolOutcome.Error);

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
