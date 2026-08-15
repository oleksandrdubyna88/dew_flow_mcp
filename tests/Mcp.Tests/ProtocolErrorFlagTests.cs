using FluentAssertions;
using Mcp.Application;
using Mcp.Contracts;
using Mcp.Server;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Protocol;
using Xunit;

namespace Mcp.Tests;

/// <summary>A refusal must reach the wire flagged, never as ordinary content.
/// <para>Found live: the first working server answered a sandbox denial with a plain text result, so a
/// caller could not tell "refused" from "read an empty file". The parent project learned the same
/// lesson measuring eval legs — an executed tool and a denied one were indistinguishable because only
/// the result's length was read.</para></summary>
public sealed class ProtocolErrorFlagTests
{
    [Fact]
    public async Task A_failed_tool_is_marked_isError_on_the_protocol_surface()
    {
        var result = await Invoke(ToolResult.Failure("outside the workspace"));

        result.IsError.Should().BeTrue();
        result.Content.OfType<TextContentBlock>().Single().Text.Should().Contain("outside the workspace");
    }

    [Fact]
    public async Task A_refused_tool_is_marked_isError_exactly_like_a_failed_one()
    {
        var result = await Invoke(ToolResult.Refusal("outside the workspace"));

        // The third case is a distinction this server keeps for ITSELF. The protocol has one error
        // state, so inventing a second on the wire would break every conforming client to express a
        // difference only telemetry needs.
        result.IsError.Should().BeTrue();
        result.Content.OfType<TextContentBlock>().Single().Text.Should().Contain("outside the workspace");
    }

    [Fact]
    public async Task A_successful_tool_is_not_marked()
    {
        var result = await Invoke(ToolResult.Success("file body"));

        result.IsError.Should().NotBe(true);
        result.Content.OfType<TextContentBlock>().Single().Text.Should().Be("file body");
    }

    private static async Task<CallToolResult> Invoke(ToolResult answer)
    {
        var catalog = ToolCatalogTests.Build(new FixedProvider(answer));
        var function = new CatalogToolFunction(catalog.Advertised[0], catalog);

        var raw = await function.InvokeAsync(new AIFunctionArguments(), TestContext.Current.CancellationToken);
        return raw.Should().BeOfType<CallToolResult>().Subject;
    }

    private sealed class FixedProvider(ToolResult answer) : IToolProvider
    {
        public IReadOnlyList<ToolSchema> Tools { get; } =
        [
            new ToolSchema
            {
                Name = "fixed",
                Description = "always answers the same way",
                InputSchema = ToolSchema.ParseSchema("""{"type":"object"}"""),
            },
        ];

        public Task<ToolResult> InvokeAsync(ToolCall call, CancellationToken cancellationToken) =>
            Task.FromResult(answer);
    }
}
