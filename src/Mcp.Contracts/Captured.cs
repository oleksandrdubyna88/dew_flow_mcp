namespace Mcp.Contracts;

/// <summary>A fact that may or may not have been obtainable.
/// <para>
/// "Not captured" and "empty" are different facts, and a telemetry record that renders an unknown as
/// an empty string — or worse, as the popular answer — turns a gap in instrumentation into a claim
/// about the caller. The MCP protocol genuinely does not carry some of what this record wants (the
/// model on the other end, for one), so the shape has to be able to say so.
/// </para></summary>
public sealed record Captured(bool WasCaptured, string Value, string Reason)
{
    public static Captured Text(string value) => new(true, value, string.Empty);

    public static Captured Unavailable(string reason) => new(false, string.Empty, reason);
}

/// <summary>The same distinction for a count. A zero that means "none" and a zero that means "nobody
/// counted" are opposite readings of one number, and only one of them is safe to aggregate.</summary>
public sealed record CapturedCount(bool WasCaptured, long Value, string Reason)
{
    public static CapturedCount Number(long value) => new(true, value, string.Empty);

    public static CapturedCount Unavailable(string reason) => new(false, 0, reason);
}
