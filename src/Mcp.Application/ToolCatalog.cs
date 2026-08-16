using System.Collections.Frozen;
using System.Diagnostics;
using Mcp.Contracts;
using Microsoft.Extensions.Logging;

namespace Mcp.Application;

/// <summary>What the catalog imposes on every call it dispatches.</summary>
public sealed record ToolCatalogOptions
{
    /// <summary>How long ONE tool call may run before the catalog stops waiting for it and answers the
    /// caller a failure. Two minutes.
    /// <para>Chosen against the two ends it sits between. It is LONGER than the per-call timeout the
    /// MCP clients apply themselves (tens of seconds), so the ceiling never cuts a call somebody is
    /// still waiting on — it exists to reclaim calls whose caller has already gone, which on stdio is
    /// otherwise unbounded: the caller's token is the only bound there, and a wedged client never
    /// cancels it. It is SHORTER than "for the life of the process", which is what a missing ceiling
    /// actually means.</para>
    /// <para>It is configuration rather than a constant because the provider families this catalog is
    /// built for do not share one honest number: a sandboxed file read is milliseconds, while a host
    /// that composes GPU-backed retrieval has legitimate minute-long calls and must raise this
    /// deliberately. <see cref="Timeout.InfiniteTimeSpan"/> is accepted and disables the ceiling — the
    /// documented pair the reliability rule requires (a reason AND a watchdog that can tell "slow"
    /// from "wedged") is then the host's to provide.</para></summary>
    public TimeSpan CallTimeout { get; init; } = TimeSpan.FromMinutes(2);
}

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
    private readonly ToolCatalogOptions options;
    private readonly IUsageSink usage;
    private readonly ICallerContext callers;
    private readonly TimeProvider clock;
    private readonly ILogger<ToolCatalog> logger;

    public ToolCatalog(
        IEnumerable<IToolProvider> providers,
        ToolCatalogOptions options,
        IUsageSink usage,
        ICallerContext callers,
        TimeProvider clock,
        ILogger<ToolCatalog> logger)
    {
        this.options = options;
        this.usage = usage;
        this.callers = callers;
        this.clock = clock;
        this.logger = logger;

        GuardAgainstAnUnusableCeiling(options);
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

        var result = await RunAsync(provider, call, cancellationToken);
        await RecordAsync(call, provider.Scope, started, result, cancellationToken);
        return result;
    }

    /// <summary>Runs one provider under this server's own ceiling, and converts anything unexpected
    /// into a value.
    /// <para>Two rules meet here. <b>Every wait has a ceiling:</b> the caller's token is the only bound
    /// a provider used to get, and on stdio that can mean forever — a client that wedges or walks away
    /// never cancels, and the call runs for the life of the process. The linked source sits at the one
    /// dispatch chokepoint, so every present and future provider inherits it without knowing.</para>
    /// <para><b>A throwing tool never takes the session:</b> <see cref="IToolProvider"/> permits a
    /// throw for a genuine infrastructure fault and the sandboxed reader takes it (a locked file, a
    /// delete between the exists-check and the read). Unguarded, that exception left the catalog before
    /// the record was written — so the one call worth investigating was the only one telemetry never
    /// saw, the exact inversion the metering here exists to prevent.</para></summary>
    private async Task<ToolResult> RunAsync(IToolProvider provider, ToolCall call, CancellationToken cancellationToken)
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(options.CallTimeout);

        try
        {
            return await provider.InvokeAsync(call, budget.Token);
        }
        catch (Exception fault) when (!cancellationToken.IsCancellationRequested)
        {
            return Recovered(call, fault, budget.IsCancellationRequested);
        }

        // A call the CALLER cancelled matches no filter and travels on as cancellation, deliberately:
        // the client is gone, and fabricating a result for it would put a call in the ledger that
        // nobody made.
    }

    /// <summary>The failure a caller reads instead of a socket that never answers.</summary>
    private ToolResult Recovered(ToolCall call, Exception fault, bool expired)
    {
        if (expired && fault is OperationCanceledException)
        {
            logger.LogWarning(
                "Tool {ToolName} passed this server's {CeilingSeconds:0.###}s per-call ceiling and was cancelled",
                call.Name, options.CallTimeout.TotalSeconds);
            return ToolResult.Failure(
                $"'{call.Name}' exceeded this server's {options.CallTimeout.TotalSeconds:0.###}s ceiling for one tool call.");
        }

        // The exception FIRST, so the log carries the stack rather than a sentence about it.
        logger.LogError(fault, "Tool {ToolName} ended in an unhandled {FaultType}; the call is answered as a failure",
            call.Name, fault.GetType().Name);
        return ToolResult.Failure($"'{call.Name}' failed: {fault.Message}");
    }

    /// <summary>Meters one call. Guarded end to end — building the record included — because telemetry
    /// may never fail a tool call: a sink that threw here would lose the very session the guard above
    /// just saved, one layer further down.</summary>
    private async Task RecordAsync(
        ToolCall call,
        string scope,
        long started,
        ToolResult result,
        CancellationToken cancellationToken)
    {
        try
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
        catch (Exception fault)
        {
            logger.LogError(fault, "Recording usage for {ToolName} failed; the call itself is unaffected", call.Name);
        }
    }

    private static ToolOutcome Outcome(ToolResult result) =>
        result.Match(_ => ToolOutcome.Answered, _ => ToolOutcome.Refused, _ => ToolOutcome.Error);

    /// <summary>A ceiling that cannot be applied stops the host at composition, where one message is
    /// read once — rather than throwing out of <c>CancelAfter</c> on every call for the life of a
    /// process nobody is watching.</summary>
    private static void GuardAgainstAnUnusableCeiling(ToolCatalogOptions options)
    {
        if (options.CallTimeout <= TimeSpan.Zero && options.CallTimeout != Timeout.InfiniteTimeSpan)
        {
            throw new InvalidOperationException(
                $"{nameof(ToolCatalogOptions)}.{nameof(ToolCatalogOptions.CallTimeout)} must be positive, "
                + $"or Timeout.InfiniteTimeSpan to disable the ceiling; got {options.CallTimeout}.");
        }
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
