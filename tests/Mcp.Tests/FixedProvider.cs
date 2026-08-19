using Mcp.Contracts;

namespace Mcp.Tests;

/// <summary>
/// A provider whose one tool always answers the same way — the shortest way to ask a presentation what it
/// does with each case of <see cref="ToolResult"/>.
///
/// <para>Extracted from <c>ProtocolErrorFlagTests</c> when <c>BridgeErrorParityTests</c> needed exactly it.
/// Two copies of a fixed provider would be two things to keep in step with one union, which is how the third
/// case comes to be tested on one surface and not the other.</para>
/// </summary>
internal sealed class FixedProvider(ToolResult answer) : IToolProvider
{
    public const string ToolName = "fixed";

    public IReadOnlyList<ToolSchema> Tools { get; } =
    [
        new ToolSchema
        {
            Name = ToolName,
            Description = "always answers the same way",
            InputSchema = ToolSchema.ParseSchema("""{"type":"object"}"""),
        },
    ];

    public Task<ToolResult> InvokeAsync(ToolCall call, CancellationToken cancellationToken) =>
        Task.FromResult(answer);
}
