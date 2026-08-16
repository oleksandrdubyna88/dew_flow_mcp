using FluentAssertions;
using Mcp.Application;
using Mcp.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Mcp.Tests;

/// <summary>The tool surface as configuration: which tools a process serves, and what it says they do.
/// <para>Measured on the previous generation and the reason this is configurable at all: rewriting ONE
/// instruction about which tool to use when moved a score 16.5 points of 63, while swapping the toolbox
/// from 4 tools to 18 moved it 1. A wording that can only change by rebuilding the binary is a wording
/// nobody runs ten variants of.</para></summary>
public sealed class ToolSurfaceTests
{
    [Fact]
    public void A_host_that_configures_nothing_advertises_every_registered_tool()
    {
        var catalog = Resolve(ToolSurfaceOptions.Everything, new PairProvider());

        // The default must not merely resemble today's behaviour; IsEverything takes the untouched
        // registration path, so it is literally the same code.
        ToolSurfaceOptions.Everything.IsEverything.Should().BeTrue();
        catalog.Advertised.Select(tool => tool.Name).Should().Equal("rt_glob", "rt_read_local_file");
    }

    [Fact]
    public void Only_the_named_tools_are_advertised()
    {
        var catalog = Resolve(Subset("rt_read_local_file"), new PairProvider());

        catalog.Advertised.Select(tool => tool.Name).Should().Equal("rt_read_local_file");
    }

    [Fact]
    public async Task A_call_to_a_tool_outside_the_surface_is_refused_rather_than_dispatched()
    {
        var inner = new PairProvider();
        var surface = new ToolSurfaceProvider(inner, Names("rt_read_local_file"), ToolDescriptionCatalog.None);

        var result = await surface.InvokeAsync(
            new ToolCall("rt_glob", ToolCatalogTests.Empty()), TestContext.Current.CancellationToken);

        // Refused, not Failed: the request was understood and answered "no". A caller reaching for a
        // tool this server does not advertise is working from a stale configuration, which is the same
        // reading a sandbox denial gets — and telemetry must be able to tell it from a broken disk.
        result.Should().BeOfType<ToolResult.Refused>()
            .Which.Reason.Should().Contain("rt_glob").And.Contain("surface");
        inner.Invoked.Should().BeEmpty("a tool outside the surface must never reach the provider");
    }

    [Fact]
    public void A_description_from_the_catalog_replaces_the_literal_the_provider_compiled_in()
    {
        var directory = DescriptionSet("concise-v1", "rt_glob", "Files by path pattern, newest first.");

        var catalog = Resolve(Descriptions(directory, "concise-v1"), new PairProvider());

        Advertised(catalog, "rt_glob").Description.Should().Be("Files by path pattern, newest first.");
        Advertised(catalog, "rt_read_local_file").Description.Should().Be(PairProvider.CompiledRead,
            "a tool with no file keeps the literal, which is the floor and not a default to be replaced");
    }

    [Fact]
    public void Overriding_a_description_carries_the_argument_schema_through_untouched()
    {
        var directory = DescriptionSet("concise-v1", "rt_glob", "a different wording entirely");

        var configured = Resolve(Descriptions(directory, "concise-v1"), new PairProvider());
        var shipped = Resolve(ToolSurfaceOptions.Everything, new PairProvider());

        // A schema that drifts when only the prose was meant to change would make every wording arm
        // measure two variables at once.
        Advertised(configured, "rt_glob").InputSchema.GetRawText()
            .Should().Be(Advertised(shipped, "rt_glob").InputSchema.GetRawText());
    }

    [Fact]
    public void A_subset_naming_a_tool_no_provider_offers_stops_the_host_naming_both_sides()
    {
        var build = () => Resolve(Subset("rt_read_local_file", "rt_grep"), new PairProvider());

        // A surface silently smaller than the one somebody configured is the failure this whole seam
        // exists to make visible; a typo must not be able to introduce it.
        build.Should().Throw<InvalidOperationException>()
            .WithMessage("*rt_grep*rt_glob*rt_read_local_file*");
    }

    [Fact]
    public void A_description_set_naming_a_tool_outside_the_subset_stops_the_host_naming_both_sides()
    {
        var directory = DescriptionSet("concise-v1", "rt_glob", "wording for a tool this host excluded");

        var build = () => Resolve(
            Descriptions(directory, "concise-v1") with { Tools = Names("rt_read_local_file") },
            new PairProvider());

        build.Should().Throw<InvalidOperationException>()
            .WithMessage("*concise-v1*rt_glob*");
    }

    [Fact]
    public void A_description_set_named_without_a_directory_to_read_it_from_stops_the_host()
    {
        var build = () => Resolve(
            ToolSurfaceOptions.From(string.Empty, string.Empty, "concise-v1"), new PairProvider());

        // It would otherwise serve every compiled default while looking configured — and the puzzle
        // would surface as two identical arms, days later.
        build.Should().Throw<InvalidOperationException>().WithMessage("*concise-v1*directory*");
    }

    [Fact]
    public void A_comma_separated_tool_list_becomes_the_subset_and_an_absent_flag_means_every_tool()
    {
        var named = ToolSurfaceOptions.From(" rt_glob , rt_read_local_file ,", string.Empty, string.Empty);
        var absent = ToolSurfaceOptions.From(string.Empty, string.Empty, string.Empty);

        named.Tools.Should().BeEquivalentTo(["rt_glob", "rt_read_local_file"]);
        absent.Tools.Should().BeEmpty();
        absent.IsEverything.Should().BeTrue();
    }

    private static ToolSchema Advertised(ToolCatalog catalog, string name) =>
        catalog.Advertised.Single(tool => tool.Name == name);

    private static ToolSurfaceOptions Subset(params string[] tools) =>
        new() { Tools = Names(tools) };

    private static ToolSurfaceOptions Descriptions(string directory, string set) =>
        new() { DescriptionsDirectory = directory, DescriptionSet = set };

    private static IReadOnlySet<string> Names(params string[] tools) =>
        new HashSet<string>(tools, StringComparer.Ordinal);

    private static string DescriptionSet(string set, string tool, string text)
    {
        var root = Directory.CreateTempSubdirectory("mcp-surface").FullName;
        var folder = Path.Combine(root, set);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, tool + ".md"), text);
        return root;
    }

    private static ToolCatalog Resolve(ToolSurfaceOptions surface, params IToolProvider[] providers)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        foreach (var provider in providers)
        {
            services.AddSingleton(provider);
        }

        services.AddMcpApplication(surface);
        return services.BuildServiceProvider().GetRequiredService<ToolCatalog>();
    }

    /// <summary>Two tools, so a subset has something to leave out.</summary>
    private sealed class PairProvider : IToolProvider
    {
        internal const string CompiledRead = "Read a file, whole or as a line window.";

        public IReadOnlyList<ToolSchema> Tools { get; } =
        [
            new ToolSchema
            {
                Name = "rt_read_local_file",
                Description = CompiledRead,
                InputSchema = ToolSchema.ParseSchema("""{"type":"object","properties":{"path":{"type":"string"}}}"""),
            },
            new ToolSchema
            {
                Name = "rt_glob",
                Description = "Match files by path pattern.",
                InputSchema = ToolSchema.ParseSchema("""{"type":"object","properties":{"pattern":{"type":"string"}}}"""),
            },
        ];

        public List<string> Invoked { get; } = [];

        public Task<ToolResult> InvokeAsync(ToolCall call, CancellationToken cancellationToken)
        {
            Invoked.Add(call.Name);
            return Task.FromResult(ToolResult.Success("done"));
        }
    }
}
