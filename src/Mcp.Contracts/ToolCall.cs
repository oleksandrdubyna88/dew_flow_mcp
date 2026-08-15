using System.Text.Json;

namespace Mcp.Contracts;

/// <summary>One invocation: the tool's name and its raw argument object.</summary>
public sealed record ToolCall(string Name, JsonElement Arguments);

/// <summary>The outcome of an invocation. A closed union, so every consumer handles every case and
/// adding one forces them to notice.
/// <para>A tool FAILURE is a value, not an exception: the protocol reports it as a result carrying
/// <c>isError</c>, and a caller that cannot tell "refused" from "returned nothing" makes exactly the
/// mistake this shape exists to prevent.</para>
/// <para><see cref="Refused"/> is the third case, and it is not a nicety. A sandbox denial and a disk
/// error are the same fact to a two-state union — which is how a read-only guarantee elsewhere was
/// asserted for months on a ledger that recorded a result's SIZE and was false the whole time. On the
/// wire both still set <c>isError</c> (the protocol has no third state); the difference is what
/// telemetry and any local consumer can see.</para></summary>
public abstract record ToolResult
{
    private ToolResult() { }

    public sealed record Ok(string Content) : ToolResult;

    /// <summary>The tool declined: a sandbox denial, a missing argument, a path outside the root. The
    /// request was understood and answered with "no" — nothing broke.</summary>
    public sealed record Refused(string Reason) : ToolResult;

    /// <summary>The tool tried and could not: an IO fault, a malformed payload, an unknown tool.</summary>
    public sealed record Failed(string Message) : ToolResult;

    public static ToolResult Success(string content) => new Ok(content);

    public static ToolResult Refusal(string reason) => new Refused(reason);

    public static ToolResult Failure(string message) => new Failed(message);

    public TOut Match<TOut>(Func<string, TOut> ok, Func<string, TOut> refused, Func<string, TOut> failed) =>
        this switch
        {
            Ok o => ok(o.Content),
            Refused r => refused(r.Reason),
            Failed f => failed(f.Message),
            _ => throw new InvalidOperationException("unreachable"),
        };

    /// <summary>The text this result carries, whichever case it is — the one projection every consumer
    /// wants and would otherwise re-write as a three-arm match of its own.</summary>
    public string Text => Match(ok => ok, refused => refused, failed => failed);
}
