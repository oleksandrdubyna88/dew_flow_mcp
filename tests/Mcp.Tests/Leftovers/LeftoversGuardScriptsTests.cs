using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace Mcp.Tests.Leftovers;

/// <summary>What the guard makes of the classifier's ANSWER, with the answer handed in as text: no process is started, no generated
/// script is run. (A stand-in for <c>node</c> written to disk and executed would be a generated artefact run outside a bounded
/// sandbox; the answer's parsed form is the thing under test, and it needs no process.)</summary>
public sealed class LeftoversGuardScriptsTests : IDisposable
{
    private static readonly string P = "testsTo" + "Delete";

    private readonly OwnedScope _owned = new();

    public void Dispose() => _owned.Dispose();

    /// <summary>A script that is NOT on disk: only the copy of its lines exists, so any answer built from it was built from the copy.</summary>
    private ScriptFile Script(string relative, params string[] lines) =>
        new(new SourceFile(relative, Path.Combine(_owned.Dir("never-written"), relative), SourceKind.Script), lines);

    private static string Verdict(ScriptFile script, int line) => Verdict(script.Source.Absolute, line);

    private static string Verdict(string file, int line) => JsonSerializer.Serialize(new { file, line });

    private static Finding Blind(string file, int line, string text = "matches the prefix with no owner segment after it (the shipped classifier)") =>
        new(file, line, "owner-blind", text);

    [Fact]
    public void A_verdict_is_answered_from_the_copy_read_once_never_from_the_file_again()
    {
        var script = Script("a.sh", $"rm -rf /tmp/{P}-*");

        var findings = GuardScripts.Judge([script], Verdict(script, 1), "node");

        findings.Should().ContainSingle().Which.Should().BeEquivalentTo(Blind("a.sh", 1));
    }

    [Fact]
    public void A_verdict_on_a_comment_line_of_the_copy_is_no_finding()
    {
        var script = Script("a.sh", $"# rm -rf /tmp/{P}-*", "echo done");

        GuardScripts.Judge([script], Verdict(script, 1), "node").Should().BeEmpty();
    }

    [Fact]
    public void The_last_line_of_the_copy_is_a_valid_verdict()
    {
        var script = Script("a.sh", "echo one", $"rm -rf /tmp/{P}-*");

        GuardScripts.Judge([script], Verdict(script, 2), "node").Should().ContainSingle().Which.Should().BeEquivalentTo(Blind("a.sh", 2));
    }
}
