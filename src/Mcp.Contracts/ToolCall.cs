using System.Text.Json;

namespace Mcp.Contracts;

/// <summary>One invocation: the tool's name and its raw argument object.</summary>
public sealed record ToolCall(string Name, JsonElement Arguments);

/// <summary>The outcome of an invocation. A closed union, so every consumer handles both cases and
/// adding a third forces them to notice.
/// <para>A tool FAILURE is a value, not an exception: the protocol reports it as a result carrying
/// <c>isError</c>, and a caller that cannot tell "refused" from "returned nothing" makes exactly the
/// mistake this shape exists to prevent.</para></summary>
public abstract record ToolResult
{
    private ToolResult() { }

    public sealed record Ok(string Content) : ToolResult;

    public sealed record Failed(string Message) : ToolResult;

    public static ToolResult Success(string content) => new Ok(content);

    public static ToolResult Failure(string message) => new Failed(message);

    public TOut Match<TOut>(Func<string, TOut> ok, Func<string, TOut> failed) =>
        this switch
        {
            Ok o => ok(o.Content),
            Failed f => failed(f.Message),
            _ => throw new InvalidOperationException("unreachable"),
        };
}
