using System.Reflection;
using FluentAssertions;
using Xunit;

namespace Mcp.Tests;

/// <summary>The rule the three-repository split rests on: nothing in this repository may know about
/// retrieval — the dependency arrow points RAG → MCP, never back. Guarded here so a violation is a
/// red build, not a review comment.</summary>
public sealed class ArchitectureTests
{
    private static readonly string[] ForbiddenPrefixes = ["Rag", "Editing", "Platform"];

    public static TheoryData<string> ShippedAssemblies()
    {
        var data = new TheoryData<string>();
        foreach (var dll in EnumerateShippedAssemblyFiles())
        {
            data.Add(Path.GetFileName(dll));
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(ShippedAssemblies))]
    public void Assembly_references_no_retrieval_types(string assemblyFile)
    {
        var referenced = Assembly
            .LoadFrom(Path.Combine(AppContext.BaseDirectory, assemblyFile))
            .GetReferencedAssemblies();

        referenced.Should().NotContain(
            r => ForbiddenPrefixes.Any(p => r.Name!.StartsWith(p, StringComparison.Ordinal)),
            "the MCP repository must know nothing about retrieval");
    }

    [Fact]
    public void Every_shipped_project_is_covered_by_the_rule()
    {
        // 8 = Contracts, Application, Server, Bridge, Api, Ui, Workspace.Application, Workspace.Infrastructure.
        // A new src project referenced by this test project lands in the output folder and is picked up
        // automatically; this floor only catches the graph silently shrinking.
        EnumerateShippedAssemblyFiles().Should().HaveCountGreaterThanOrEqualTo(8);
    }

    private static IEnumerable<string> EnumerateShippedAssemblyFiles() =>
        Directory
            .EnumerateFiles(AppContext.BaseDirectory, "*.dll")
            .Where(f => Path.GetFileName(f).StartsWith("Mcp.", StringComparison.Ordinal)
                        || Path.GetFileName(f).StartsWith("Workspace.", StringComparison.Ordinal))
            .Where(f => Path.GetFileNameWithoutExtension(f) != "Mcp.Tests");
}
