using System.Text.Json;
using FluentAssertions;
using Mcp.Application;
using Mcp.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Mcp.Tests;

/// <summary>Declare and echo, never assume. What a configuration ASKED for and what a process is
/// SERVING are two different facts, and only the second one explains a result — so the fingerprint is
/// built from what the catalog advertises, never from the files it was told to read.</summary>
public sealed class SurfaceFingerprintTests
{
    [Fact]
    public void The_echo_is_the_text_actually_advertised_and_not_the_file_it_came_from()
    {
        var directory = DescriptionSet("concise-v1", "rt_glob", "  Files by path pattern.  ");

        var host = Built(Configured(directory, "concise-v1"));
        var fingerprint = host.GetRequiredService<SurfaceFingerprintReader>().Read();

        // Asserted against the advertised list rather than against the file: the file is the request,
        // the catalog is the answer, and the whole point of an echo is that those can differ. Here they
        // differ by the trim, which is why the raw file text carries the surrounding whitespace.
        var advertised = host.GetRequiredService<ToolCatalog>().Advertised
            .Single(tool => tool.Name == "rt_glob").Description;

        fingerprint.Tools.Single(tool => tool.Name == "rt_glob").Description.Should().Be(advertised);
        advertised.Should().Be("Files by path pattern.");
        fingerprint.DescriptionSet.Should().Be("concise-v1");
    }

    [Fact]
    public void The_hashes_are_stable_across_two_reads_of_an_unchanged_configuration()
    {
        var directory = DescriptionSet("concise-v1", "rt_glob", "one wording");

        var first = Reader(Configured(directory, "concise-v1")).Read();
        var second = Reader(Configured(directory, "concise-v1")).Read();

        // Two separately built processes over one configuration must agree, or the hash cannot be the
        // thing a run records to prove which surface produced its numbers.
        second.ToolsHash.Should().Be(first.ToolsHash);
        second.DescriptionsHash.Should().Be(first.DescriptionsHash);
        second.Tools.Select(t => t.SchemaHash).Should().Equal(first.Tools.Select(t => t.SchemaHash));
    }

    [Fact]
    public void A_changed_description_changes_the_descriptions_hash_and_leaves_the_tools_hash_alone()
    {
        var before = Reader(Configured(DescriptionSet("s", "rt_glob", "wording A"), "s")).Read();
        var after = Reader(Configured(DescriptionSet("s", "rt_glob", "wording B"), "s")).Read();

        // The two hashes answer different questions — "which tools" and "what do they say" — and an
        // arm that varies only the wording must be visible as exactly that.
        after.DescriptionsHash.Should().NotBe(before.DescriptionsHash);
        after.ToolsHash.Should().Be(before.ToolsHash);
        Echo(after, "rt_glob").SchemaHash.Should().Be(Echo(before, "rt_glob").SchemaHash,
            "changing prose must not appear as a schema change");
    }

    [Fact]
    public void A_smaller_subset_changes_the_tools_hash()
    {
        var whole = Reader(ToolSurfaceOptions.Everything).Read();
        var narrowed = Reader(new ToolSurfaceOptions { Tools = Only("rt_glob") }).Read();

        narrowed.ToolsHash.Should().NotBe(whole.ToolsHash);
        narrowed.Tools.Select(t => t.Name).Should().Equal("rt_glob");
    }

    [Fact]
    public void The_fingerprint_names_the_process_that_answered()
    {
        var fingerprint = Reader(ToolSurfaceOptions.Everything, app: "mcp-stdio").Read();

        // One machine runs several of these hosts; an echo that cannot say which one wrote it is an
        // echo two deployments share.
        fingerprint.App.Should().Be("mcp-stdio");
        fingerprint.Pid.Should().Be(Environment.ProcessId);
        fingerprint.TakenAt.Should().Be(Clock);
    }

    [Fact]
    public void A_version_the_assembly_does_not_declare_reads_as_not_captured_rather_than_blank()
    {
        var version = Reader(ToolSurfaceOptions.Everything).Read().Version;

        // The plan asked for a BuiltAt timestamp here. Deterministic builds replace the PE link
        // timestamp with a content hash, so a "build time" would be unobtainable or invented — and a
        // rendered value the thing itself never answered is exactly what Captured exists to prevent.
        if (version.WasCaptured)
        {
            version.Value.Should().NotBeNullOrWhiteSpace();
            version.Reason.Should().BeEmpty();
            return;
        }

        version.Value.Should().BeEmpty();
        version.Reason.Should().NotBeEmpty("an unknown version must say why it is unknown");
    }

    [Fact]
    public void The_fingerprint_serializes_to_json_a_consumer_can_read()
    {
        var json = SurfaceJson.Text(Reader(ToolSurfaceOptions.Everything).Read());

        using var parsed = JsonDocument.Parse(json);
        var tools = parsed.RootElement.GetProperty("tools");
        tools.GetArrayLength().Should().Be(2);
        tools[0].GetProperty("name").GetString().Should().Be("rt_glob");
        tools[0].GetProperty("description").GetString().Should().NotBeNullOrEmpty();
        tools[0].GetProperty("schemaHash").GetString().Should().NotBeNullOrEmpty();
        parsed.RootElement.GetProperty("toolsHash").GetString().Should().HaveLength(64);
        parsed.RootElement.GetProperty("descriptionsHash").GetString().Should().HaveLength(64);
    }

    private static readonly DateTimeOffset Clock = new(2026, 8, 16, 12, 0, 0, TimeSpan.Zero);

    private static ToolDescriptionEcho Echo(SurfaceFingerprint fingerprint, string tool) =>
        fingerprint.Tools.Single(entry => entry.Name == tool);

    private static ToolSurfaceOptions Configured(string directory, string set) =>
        new() { DescriptionsDirectory = directory, DescriptionSet = set };

    private static IReadOnlySet<string> Only(params string[] tools) =>
        new HashSet<string>(tools, StringComparer.Ordinal);

    private static string DescriptionSet(string set, string tool, string text)
    {
        var root = Directory.CreateTempSubdirectory("mcp-fingerprint").FullName;
        var folder = Path.Combine(root, set);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, tool + ".md"), text);
        return root;
    }

    private static SurfaceFingerprintReader Reader(ToolSurfaceOptions surface, string app = "mcp-test") =>
        Built(surface, app).GetRequiredService<SurfaceFingerprintReader>();

    private static IServiceProvider Built(ToolSurfaceOptions surface, string app = "mcp-test")
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<TimeProvider>(new FakeTimeProvider(Clock));
        services.AddSingleton<IToolProvider>(new PairProvider());
        services.AddMcpApplication(surface);
        services.AddSurfaceFingerprint(app);
        return services.BuildServiceProvider();
    }

    private sealed class PairProvider : IToolProvider
    {
        public IReadOnlyList<ToolSchema> Tools { get; } =
        [
            new ToolSchema
            {
                Name = "rt_read_local_file",
                Description = "Read a file, whole or as a line window.",
                InputSchema = ToolSchema.ParseSchema("""{"type":"object","properties":{"path":{"type":"string"}}}"""),
            },
            new ToolSchema
            {
                Name = "rt_glob",
                Description = "Match files by path pattern.",
                InputSchema = ToolSchema.ParseSchema("""{"type":"object","properties":{"pattern":{"type":"string"}}}"""),
            },
        ];

        public Task<ToolResult> InvokeAsync(ToolCall call, CancellationToken cancellationToken) =>
            Task.FromResult(ToolResult.Success("done"));
    }
}
