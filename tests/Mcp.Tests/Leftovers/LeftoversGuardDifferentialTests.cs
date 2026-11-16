using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace Mcp.Tests.Leftovers;

/// <summary>The ONE live check that holds the C# guard and the Node guard to each other (the testing rule: a contract with two implementations is
/// not proven by two suites that each compare their own list). The shared table of <see cref="GuardScenarios"/> is fed to the C# guard
/// (<see cref="LeftoversGuard.Scan"/>) and to the MOUNTED Node guard (<c>.agents/conventions/tools/leftovers-check.mjs</c>, <c>scanText</c>, through
/// <c>node</c> with the rows on standard input), and the findings — line, class and text — must be equal, row by row.
/// <para>The mounted guard is the release this repository's rules pin names. While that predates F41 (the module does not export
/// <c>DECLARATION_KINDS</c>) it does not know the <c>container-scoped</c> marker, so the marker rows are skipped with the reason and the rows
/// that do not depend on F41 still run; when the pin reaches the F41 release the skip ends by itself.</para></summary>
public sealed class LeftoversGuardDifferentialTests : IDisposable
{
    private const string Reason = "the mounted guard predates F41 (it does not export DECLARATION_KINDS); the marker rows of this check run after mcp's rules pin reaches the F41 release";

    private const string Program = """
        import { readFileSync } from "node:fs";
        const [module] = process.argv.slice(1);
        const guard = await import(module);
        const rows = JSON.parse(readFileSync(0, "utf8"));
        const results = rows.map((row) => guard.scanText(row.file, row.text).map(({ line, cls, text }) => ({ line, cls, text })));
        console.log(JSON.stringify({ f41: "DECLARATION_KINDS" in guard, results }));
        """;

    private readonly OwnedScope _owned = new();

    public void Dispose() => _owned.Dispose();

    private sealed record Answer(int Line, string Class, string Text)
    {
        public override string ToString() => $"{Line}:{Class}:{Text}";
    }

    private sealed record NodeAnswer(bool F41, List<List<NodeFinding>> Results);

    private sealed record NodeFinding(int Line, string Cls, string Text);

    [Fact]
    public void The_table_has_rows_for_both_halves_and_every_row_is_shared_or_says_why_not()
    {
        GuardScenarios.All.Count(s => s.Marked).Should().BeGreaterThan(40, "the marker half is not empty");
        GuardScenarios.All.Count(s => !s.Marked).Should().BeGreaterThan(5, "the half that does not depend on F41 is not empty");
        GuardScenarios.All.Where(s => s.CSharpOnly is not null).Should().OnlyContain(s => s.CSharpOnly!.Length > 20, "a row kept from Node names the difference");
        GuardScenarios.All.Select(s => s.Id).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Lines_that_do_not_depend_on_the_marker_read_the_same_in_the_mounted_node_guard()
    {
        var rows = GuardScenarios.All.Where(s => !s.Marked && s.CSharpOnly is null).ToList();

        var (_, node) = AskNode(rows);

        Disagreements(rows, node).Should().BeEmpty("the C# guard and the mounted Node guard must answer alike for every row");
    }

    [Fact]
    public void Marker_lines_read_the_same_in_the_mounted_node_guard_once_it_knows_the_marker()
    {
        var rows = GuardScenarios.All.Where(s => s.Marked && s.CSharpOnly is null).ToList();

        var (f41, node) = AskNode(rows);
        if (!f41)
        {
            Assert.Skip(Reason);
            return;
        }

        Disagreements(rows, node).Should().BeEmpty("the C# guard and the mounted Node guard must answer alike for every marker row");
    }

    private (bool F41, List<List<Answer>> Results) AskNode(List<GuardScenario> rows)
    {
        var module = new Uri(Path.GetFullPath(Path.Combine(RepositoryRoot.Path, ".agents/conventions/tools/leftovers-check.mjs"))).AbsoluteUri;
        var input = JsonSerializer.Serialize(rows.Select(r => new { file = r.File, text = r.Text }));
        var json = LeftoversGuardTests.RunNode(Program, [module], TimeSpan.FromMinutes(2), input: input);
        var answer = JsonSerializer.Deserialize<NodeAnswer>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? throw new InvalidOperationException("node answered nothing");
        answer.Results.Should().HaveCount(rows.Count);
        return (answer.F41, [.. answer.Results.Select(r => r.Select(f => new Answer(f.Line, f.Cls, f.Text)).Order(Comparer<Answer>.Create(Compare)).ToList())]);
    }

    private static int Compare(Answer a, Answer b) => a.Line != b.Line ? a.Line.CompareTo(b.Line) : string.CompareOrdinal(a.Class, b.Class);

    /// <summary>The classes whose finding TEXT is part of the shared contract (F41/F42: the marker's refusal sentence and the teardown-or-reset text).
    /// Every other class is compared by line and class only: its wording predates the contract and differs on purpose (each guard's header).</summary>
    private static readonly HashSet<string> TextIsContract = new(StringComparer.Ordinal) { "shared-store", "exemption" };

    /// <summary>What is compared of a finding: line and class always, the text only for a class in <see cref="TextIsContract"/>.</summary>
    private static Answer Compared(Answer a) => TextIsContract.Contains(a.Class) ? a : a with { Text = "" };

    /// <summary>The rows on which the two guards differ, each with both answers (the C# guard's is read from a tree of the row's own file).</summary>
    private List<string> Disagreements(List<GuardScenario> rows, List<List<Answer>> node) =>
        [.. rows.Select((row, i) => (row, ours: CSharpAnswer(row), theirs: node[i])).Where(r => !r.ours.Select(Compared).SequenceEqual(r.theirs.Select(Compared)))
            .Select(r => $"{r.row.Id}: C# [{string.Join("; ", r.ours)}] / Node [{string.Join("; ", r.theirs)}]")];

    private List<Answer> CSharpAnswer(GuardScenario row)
    {
        var root = _owned.Dir("differential");
        var full = Path.Combine(root, row.File);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, row.Text);
        var report = LeftoversGuard.Scan(root, RepositoryRoot.Path);
        return [.. report.Findings.Select(f => new Answer(f.Line, f.Class, f.Text)).Order(Comparer<Answer>.Create(Compare))];
    }
}
