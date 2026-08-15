using System.Text.Json;
using FluentAssertions;
using Mcp.Application;
using Mcp.Bridge;
using Mcp.Contracts;
using Mcp.Server;
using ModelContextProtocol.Server;
using Workspace.Application;
using Xunit;

namespace Mcp.Tests;

/// <summary>The guarantee the three-repository split rests on: the protocol server and the local-LLM
/// bridge are two PRESENTATIONS of one catalog, never two implementations.
/// <para>Before this contract existed, the same tools were written twice and the two surfaces drifted
/// far enough to score 11/47 against 4/47 on identical tasks. These tests are what make that
/// unbuildable — they fail the moment either presentation grows a tool of its own.</para></summary>
public sealed class SurfaceParityTests
{
    [Fact]
    public void The_protocol_and_the_bridge_advertise_exactly_the_same_tools()
    {
        var catalog = BuildWorkspaceCatalog(out _);

        var protocolNames = ProtocolToolNames(catalog);
        var bridgeNames = Bridge(catalog).ToolDefinitions.Select(d => d.Function.Name);

        bridgeNames.Should().Equal(protocolNames);
    }

    [Fact]
    public void Both_presentations_publish_the_identical_argument_schema()
    {
        var catalog = BuildWorkspaceCatalog(out _);
        var bridge = Bridge(catalog);

        foreach (var advertised in catalog.Advertised)
        {
            var fromBridge = bridge.ToolDefinitions.Single(d => d.Function.Name == advertised.Name);
            fromBridge.Function.Parameters.GetRawText()
                .Should().Be(advertised.InputSchema.GetRawText(),
                    "a model must see one schema for {0}, whichever surface it arrives through", advertised.Name);
        }
    }

    [Fact]
    public async Task A_tool_invoked_through_the_bridge_runs_the_same_code_the_protocol_runs()
    {
        var catalog = BuildWorkspaceCatalog(out var root);
        var bridge = Bridge(catalog);
        await File.WriteAllTextAsync(Path.Combine(root, "note.txt"), "alpha\nbeta", TestContext.Current.CancellationToken);

        var viaBridge = await bridge.InvokeAsync(
            WorkspaceToolProvider.ReadLocalFile,
            """{"path":"note.txt"}""",
            TestContext.Current.CancellationToken);

        var viaCatalog = await catalog.InvokeAsync(
            new ToolCall(WorkspaceToolProvider.ReadLocalFile,
                JsonDocument.Parse("""{"path":"note.txt"}""").RootElement.Clone()),
            TestContext.Current.CancellationToken);

        // Record value equality: same case, same content — one code path, reached two ways.
        viaBridge.Should().Be(viaCatalog);
        viaBridge.Should().BeOfType<ToolResult.Ok>().Which.Content.Should().Contain("alpha");
    }

    [Fact]
    public async Task The_bridge_reports_malformed_arguments_as_a_tool_failure_the_model_can_retry()
    {
        var catalog = BuildWorkspaceCatalog(out _);
        var bridge = Bridge(catalog);

        // Local models emit broken JSON regularly; aborting the loop over it would be the wrong answer.
        var result = await bridge.InvokeAsync(
            WorkspaceToolProvider.ReadLocalFile, "{not json", TestContext.Current.CancellationToken);

        result.Should().BeOfType<ToolResult.Failed>()
            .Which.Message.Should().Contain("not valid JSON");
    }

    [Fact]
    public void A_new_provider_reaches_both_surfaces_with_no_edit_inside_the_mcp_module()
    {
        var catalog = ToolCatalogTests.Build(new ExtraProvider());

        ProtocolToolNames(catalog).Should().Contain("brand_new_tool");
        Bridge(catalog).ToolDefinitions
            .Select(d => d.Function.Name).Should().Contain("brand_new_tool");
    }

    /// <summary>The bridge as a host with no model to declare — the honest default, and the one that
    /// records the model as not captured rather than inventing one.</summary>
    private static LocalLlmToolBridge Bridge(ToolCatalog catalog) =>
        new(catalog, new AmbientCallerContext(), BridgeCaller.Driving(string.Empty));

    private static IEnumerable<string> ProtocolToolNames(ToolCatalog catalog) =>
        catalog.Advertised
            .Select(schema => McpServerTool.Create(new CatalogToolFunction(schema, catalog)))
            .Select(tool => tool.ProtocolTool.Name);

    private static ToolCatalog BuildWorkspaceCatalog(out string root)
    {
        root = Directory.CreateTempSubdirectory("mcp-parity").FullName;
        var reader = new Workspace.Infrastructure.SandboxedFileReader(
            new Workspace.Infrastructure.WorkspaceRoot(root),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<Workspace.Infrastructure.SandboxedFileReader>.Instance);
        return ToolCatalogTests.Build(new WorkspaceToolProvider(reader));
    }

    private sealed class ExtraProvider : IToolProvider
    {
        public IReadOnlyList<ToolSchema> Tools { get; } =
        [
            new ToolSchema
            {
                Name = "brand_new_tool",
                Description = "added without touching Mcp.Server or Mcp.Bridge",
                InputSchema = ToolSchema.ParseSchema("""{"type":"object"}"""),
            },
        ];

        public Task<ToolResult> InvokeAsync(ToolCall call, CancellationToken cancellationToken) =>
            Task.FromResult(ToolResult.Success("ok"));
    }
}
