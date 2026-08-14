namespace Mcp.Contracts;

/// <summary>Where per-call usage goes. A PORT on purpose: this repository is public, so it must not
/// carry a hardcoded telemetry destination. The default sink discards; the product implements a real
/// one without this module ever learning where the data lands.</summary>
public interface IUsageSink
{
    Task RecordAsync(ToolUsage usage, CancellationToken cancellationToken);
}

/// <summary>What one tool call cost. Deliberately free of arguments, results and paths — this is a
/// counter, and it must stay one.</summary>
public sealed record ToolUsage(string ToolName, TimeSpan Duration, bool Failed, int ResponseChars);

/// <summary>The default: records nothing. Chosen so that forgetting to register a sink loses telemetry
/// rather than breaking tool calls.</summary>
public sealed class NullUsageSink : IUsageSink
{
    public Task RecordAsync(ToolUsage usage, CancellationToken cancellationToken) => Task.CompletedTask;
}
