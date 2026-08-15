namespace Mcp.Contracts;

/// <summary>Where per-call usage goes. A PORT on purpose: this repository is public, so it must not
/// carry a hardcoded telemetry destination. The default sink discards; the product implements a real
/// one without this module ever learning where the data lands.</summary>
public interface IUsageSink
{
    Task RecordAsync(ToolUsage usage, CancellationToken cancellationToken);
}

/// <summary>How a call ended. Three states, because a tool that declined and a tool that broke are
/// different events with different remedies, and a boolean merges them permanently.</summary>
public enum ToolOutcome
{
    Answered,
    Refused,
    Error,
}

/// <summary>What one tool call was, and what it cost — the emitter half of the benchmark-owned
/// <c>telemetry/v0</c> schema.
/// <para>
/// This record used to be a bare counter (name, duration, failed, chars) and said so in its own
/// comment. It stopped being one deliberately: a counter answers "how much", and every question worth
/// asking of a tool surface is "by whom, against what, with which arguments, and did it actually
/// answer". Payloads are byte-budgeted by the CALLER of this port before they arrive, so the budget is
/// a property of the record rather than of a later clean-up job.
/// </para></summary>
public sealed record ToolUsage(
    string ToolName,
    DateTimeOffset At,
    CallerIdentity Caller,
    string Scope,
    string ArgumentsJson,
    int ArgumentsTruncatedBytes,
    ToolOutcome Outcome,
    string Error,
    int ResponseChars,
    string ResponseBody,
    int ResponseTruncatedBytes,
    CapturedCount Tokens,
    TimeSpan Duration);

/// <summary>The default: records nothing. Chosen so that forgetting to register a sink loses telemetry
/// rather than breaking tool calls.</summary>
public sealed class NullUsageSink : IUsageSink
{
    public Task RecordAsync(ToolUsage usage, CancellationToken cancellationToken) => Task.CompletedTask;
}
