using FluentAssertions;
using Mcp.Application;
using Mcp.Bridge;
using Mcp.Contracts;
using Mcp.Server;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Protocol;
using Xunit;

namespace Mcp.Tests;

/// <summary>
/// A refused call is as distinguishable on the BRIDGE as <c>isError</c> makes it on the protocol.
///
/// <para><see cref="ProtocolErrorFlagTests"/> pins the wire, and <see cref="SurfaceParityTests"/> pins the
/// names and the schemas — so the two presentations were known to advertise the same surface and to run the
/// same code. Nothing checked that they SIGNAL A FAILURE the same way, and that is the half where the
/// original defect lived: the first working server answered a sandbox denial with a plain text result, and a
/// caller could not tell "refused" from "read an empty file". The wire is guarded against that now. This is
/// the same guarantee for the presentation a local model reaches.</para>
///
/// <para>The two surfaces are deliberately NOT identical here, and that asymmetry is what these tests pin:
/// the protocol has one error state, so refused and failed collapse into one flag on the wire; the bridge
/// hands over the union itself and keeps all three. More information on one side is fine — an error arriving
/// as a SUCCESS on either side is not.</para>
/// </summary>
public sealed class BridgeErrorParityTests
{
    [Fact]
    public async Task A_refusal_reaches_the_bridge_as_a_refusal_and_never_as_content()
    {
        var result = await ViaBridge(ToolResult.Refusal("outside the workspace"));

        // The exact inversion this family of tests exists for: as an Ok, the reason becomes the answer, and a
        // working sandbox guard reads as an empty file.
        result.Should().BeOfType<ToolResult.Refused>()
            .Which.Reason.Should().Contain("outside the workspace");
        result.Should().NotBeOfType<ToolResult.Ok>();
    }

    [Fact]
    public async Task A_failure_reaches_the_bridge_as_a_failure()
    {
        var result = await ViaBridge(ToolResult.Failure("the file is locked"));

        result.Should().BeOfType<ToolResult.Failed>()
            .Which.Message.Should().Contain("the file is locked");
    }

    [Fact]
    public async Task A_success_is_not_marked_on_either_surface()
    {
        var answer = ToolResult.Success("file body");

        (await ViaBridge(answer)).Should().BeOfType<ToolResult.Ok>();
        (await ViaProtocol(answer)).IsError.Should().NotBe(true);
    }

    [Theory]
    [InlineData("refused")]
    [InlineData("failed")]
    public async Task Both_surfaces_agree_that_something_went_wrong_whichever_case_it_was(string kind)
    {
        var answer = kind == "refused"
            ? ToolResult.Refusal("outside the workspace")
            : ToolResult.Failure("the file is locked");

        var onTheWire = await ViaProtocol(answer);
        var inProcess = await ViaBridge(answer);

        // The agreement that matters: neither surface may report this as an answer.
        onTheWire.IsError.Should().BeTrue();
        inProcess.Should().NotBeOfType<ToolResult.Ok>();

        // And the same text travels, so a caller reading either surface gets the reason rather than a
        // sanitised placeholder.
        onTheWire.Content.OfType<TextContentBlock>().Single().Text.Should().Be(inProcess.Text);
    }

    [Fact]
    public async Task The_bridge_keeps_the_third_case_the_wire_cannot_carry()
    {
        // The protocol has one error state, so this distinction cannot exist on the wire — and it must not be
        // thrown away in process, because "the guard said no" and "the disk broke" are the two facts an audit
        // of this surface is FOR. A ledger that filed both under one flag is how a read-only guarantee was
        // asserted for months elsewhere while being false.
        var refused = await ViaBridge(ToolResult.Refusal("outside the workspace"));
        var failed = await ViaBridge(ToolResult.Failure("outside the workspace"));

        refused.Should().NotBe(failed, "same text, different fact");
        refused.Should().BeOfType<ToolResult.Refused>();
        failed.Should().BeOfType<ToolResult.Failed>();

        var wire = await ViaProtocol(ToolResult.Refusal("outside the workspace"));
        var wireFailed = await ViaProtocol(ToolResult.Failure("outside the workspace"));
        wire.IsError.Should().Be(wireFailed.IsError,
            "on the wire the two ARE one state, deliberately — inventing a second would break every "
            + "conforming client to express a difference only telemetry needs");
    }

    private static async Task<ToolResult> ViaBridge(ToolResult answer)
    {
        var catalog = ToolCatalogTests.Build(new FixedProvider(answer));
        var bridge = new LocalLlmToolBridge(catalog, new AmbientCallerContext(), BridgeCaller.Driving("test-model"));

        return await bridge.InvokeAsync(FixedProvider.ToolName, "{}", TestContext.Current.CancellationToken);
    }

    private static async Task<CallToolResult> ViaProtocol(ToolResult answer)
    {
        var catalog = ToolCatalogTests.Build(new FixedProvider(answer));
        var function = new CatalogToolFunction(catalog.Advertised[0], catalog);

        var raw = await function.InvokeAsync(new AIFunctionArguments(), TestContext.Current.CancellationToken);
        return raw.Should().BeOfType<CallToolResult>().Subject;
    }
}
