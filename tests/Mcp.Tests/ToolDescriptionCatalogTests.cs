using FluentAssertions;
using Mcp.Application;
using Xunit;

namespace Mcp.Tests;

/// <summary>The rule this catalog exists for: a description can change without a rebuild, and the
/// compiled literal is the FLOOR — never something a file can take away.</summary>
public sealed class ToolDescriptionCatalogTests
{
    private const string BuiltIn = "the description compiled into the provider";

    [Fact]
    public void A_named_set_overrides_the_compiled_description()
    {
        var directory = Sets(("concise-v1", "rt_read_local_file", "Read a window of a file."));

        var catalog = ToolDescriptionCatalog.Load(directory, "concise-v1");

        catalog.DescriptionFor("rt_read_local_file", BuiltIn).Should().Be("Read a window of a file.");
    }

    [Fact]
    public void A_tool_with_no_file_keeps_the_compiled_description()
    {
        var directory = Sets(("concise-v1", "some_other_tool", "not the tool being asked about"));

        var catalog = ToolDescriptionCatalog.Load(directory, "concise-v1");

        catalog.DescriptionFor("rt_read_local_file", BuiltIn).Should().Be(BuiltIn);
    }

    [Fact]
    public void A_blank_file_falls_back_to_the_compiled_description_rather_than_advertising_nothing()
    {
        var directory = Sets(("concise-v1", "rt_read_local_file", "   \n  "));

        var catalog = ToolDescriptionCatalog.Load(directory, "concise-v1");

        // A tool with no description is a tool no agent can route to, and an empty file is a far
        // likelier accident than a deliberate silence.
        catalog.DescriptionFor("rt_read_local_file", BuiltIn).Should().Be(BuiltIn);
        catalog.NamedTools.Should().BeEmpty("a blank file overrides nothing");
        catalog.Ignored.Should().ContainSingle()
            .Which.Should().Contain("rt_read_local_file").And.Contain("blank",
                "a file somebody wrote and this server ignored must not disappear silently");
    }

    [Fact]
    public void An_unknown_set_is_refused_by_name_and_the_message_names_the_sets_that_exist()
    {
        var directory = Sets(
            ("concise-v1", "rt_read_local_file", "short"),
            ("behavioural-v1", "rt_read_local_file", "long"));

        var load = () => ToolDescriptionCatalog.Load(directory, "consise-v1");

        // Silently serving the defaults for a typo'd set is exactly the invisible failure this whole
        // seam exists to prevent: the A/B would run, and both arms would be the same arm.
        load.Should().Throw<InvalidOperationException>()
            .WithMessage("*consise-v1*behavioural-v1*concise-v1*");
    }

    [Fact]
    public void An_unnamed_set_reads_the_directory_itself()
    {
        var directory = Sets(("", "rt_read_local_file", "straight from the root"));

        var catalog = ToolDescriptionCatalog.Load(directory, string.Empty);

        catalog.DescriptionFor("rt_read_local_file", BuiltIn).Should().Be("straight from the root");
        catalog.Set.Should().BeEmpty();
    }

    [Fact]
    public void A_set_does_not_see_the_files_of_the_directory_around_it()
    {
        var directory = Sets(
            ("", "rt_read_local_file", "the root's wording"),
            ("concise-v1", "some_other_tool", "irrelevant"));

        var catalog = ToolDescriptionCatalog.Load(directory, "concise-v1");

        // Two sets that leak into each other are two arms measuring one population.
        catalog.DescriptionFor("rt_read_local_file", BuiltIn).Should().Be(BuiltIn);
    }

    [Fact]
    public void No_directory_at_all_leaves_every_description_as_the_compiled_literal()
    {
        var catalog = ToolDescriptionCatalog.Load(string.Empty, string.Empty);

        // What a customer who names no catalog gets: today's binary, byte for byte.
        catalog.Should().BeSameAs(ToolDescriptionCatalog.None);
        catalog.DescriptionFor("rt_read_local_file", BuiltIn).Should().Be(BuiltIn);
        catalog.NamedTools.Should().BeEmpty();
    }

    [Fact]
    public void A_directory_that_does_not_exist_is_refused_rather_than_read_as_empty()
    {
        var missing = Path.Combine(Directory.CreateTempSubdirectory("mcp-descriptions").FullName, "nope");

        var load = () => ToolDescriptionCatalog.Load(missing, string.Empty);

        load.Should().Throw<InvalidOperationException>().WithMessage("*does not exist*");
    }

    /// <summary>Lays out <c>&lt;temp&gt;/&lt;set&gt;/&lt;tool&gt;.md</c>; an empty set writes to the
    /// root.</summary>
    private static string Sets(params (string Set, string Tool, string Text)[] files)
    {
        var root = Directory.CreateTempSubdirectory("mcp-descriptions").FullName;
        foreach (var (set, tool, text) in files)
        {
            var folder = Path.Combine(root, set);
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, tool + ".md"), text);
        }

        return root;
    }
}
