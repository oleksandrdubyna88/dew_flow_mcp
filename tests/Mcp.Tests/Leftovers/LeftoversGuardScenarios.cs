namespace Mcp.Tests.Leftovers;

/// <summary>One scenario of the guard's table: a file path, its text, the classes the C# guard must find (sorted, comma-joined; empty is clean).
/// <see cref="Id"/> and <see cref="Text"/> are already without the section sign that keeps the detector words out of this file.
/// <see cref="CSharpOnly"/> says why the scenario is NOT given to the Node guard: a difference the C# guard states on purpose.</summary>
internal sealed record GuardScenario(string Id, string File, string Text, string Expected, string? CSharpOnly = null)
{
    /// <summary>Does the text carry a <c>container-scoped</c> marker? The mounted Node guard reads that only from its F41 release on.</summary>
    public bool Marked => Text.Contains("container-scoped", StringComparison.Ordinal);
}

/// <summary>The marker (rule §6 (b)) and shared-store scenarios — F41's P1-P7 and N1-N19 in C#, the parity rows with the Node guard, and the unmarked
/// shared-store lines. ONE table, read twice: the per-row tests of <c>LeftoversGuardTests</c> (what the C# guard answers) and the differential test
/// against the MOUNTED Node guard (<c>LeftoversGuardDifferentialTests</c>: classes, lines and texts must agree, row by row), so that a contract with two
/// implementations is held by one live check and not by two hand-copied lists. Every detector word is written with a section sign inside it, and the
/// prefix word is a variable, so that the guard scans this file clean.</summary>
internal static class GuardScenarios
{
    private static readonly string P = LeftoversGuard.CamelWord;
    private static readonly string Lower = LeftoversGuard.Word;
    private static readonly string Marker = P + "-exempt:";
    private const string Owner = "the per-test databases live and die with the PostgresFixture container";

    /// <summary>Every scenario, built once.</summary>
    internal static IReadOnlyList<GuardScenario> All { get; } = Build();

    private static string Scoped(string reason) => $"{Marker} container-scoped — {reason}";

    private static string Fx(string text) => text.Replace("§", "", StringComparison.Ordinal);

    private static List<GuardScenario> Build()
    {
        var rows = new Rows();
        AcceptedAndRefused(rows);
        ParityWithNode(rows);
        UnmarkedSharedStore(rows);
        return rows.Items;
    }

    /// <summary>What the groups write rows with.</summary>
    private sealed class Rows
    {
        internal List<GuardScenario> Items { get; } = [];

        internal void Add(string id, string file, string text, string expected) => Items.Add(new GuardScenario(Fx(id), file, Fx(text), expected));

        internal void AddCSharpOnly(string id, string file, string text, string expected, string reason) => Items.Add(new GuardScenario(Fx(id), file, Fx(text), expected, reason));
    }

    /// <summary>P1-P7 clean, N1-N19 red: the marker over each class, each refusal.</summary>
    private static void AcceptedAndRefused(Rows rows)
    {
        rows.Add("F42 P1 the marker alone, singular", "tests/X/A.cs", $"// {Scoped("the cache lives and dies with the PostgresFixture fixture")}\nvar a = 1;", "");
        rows.Add("F42 P2 the marker alone, plural", "tests/X/A.cs", $"// {Scoped(Owner)}\nvar a = 1;", "");
        rows.Add("F42 P1 an owner with no article", "tests/X/A.cs", $"// {Scoped("the data lives and dies with PostgresFixture")}\nvar a = 1;", "");
        rows.Add("F42 P1 every article is read", "tests/X/A.cs", $"// {Scoped("the data lives and dies with an Orders fixture")}\n// {Scoped("the copy lives and dies with this fixture")}\n// {Scoped("the data live and die with that container")}\n// {Scoped("the volume lives and dies with its job")}\nvar a = 1;", "");
        rows.Add("F42 P1 the spelling is read in any case", "tests/X/A.cs", $"// {Scoped("The Data Lives And Dies With The Fixture")}\nvar a = 1;", "");
        rows.Add("F42 P3 over DR§OP DATABASE, in a shell script", "a.sh", $"# {Scoped(Owner)}\npsql -c 'DR§OP DATABASE bench_case'", "");
        rows.Add("F42 P3 over DR§OP DATABASE, in a C# file", "tests/X/A.cs", $"// {Scoped(Owner)}\nawait Run(\"DR§OP DATABASE bench_case\");", "");
        rows.Add("F42 P3 over DR§OP DATABASE, in a node script", ".github/scripts/a.mjs", $"// {Scoped(Owner)}\nawait q('DR§OP DATABASE bench_case');", "");
        rows.Add("F42 P3 without the marker the same line is still shared-store", "tests/X/A.cs", "await Run(\"DR§OP DATABASE bench_case\");", "shared-store");
        rows.Add("F42 P4 over DR§OP SCHEMA", "a.sh", $"# {Scoped(Owner)}\npsql -c 'DR§OP SCHEMA IF EXISTS bench_case'", "");
        rows.Add("F42 P5 over FLUSH§DB", "a.sh", $"# {Scoped(Owner)}\nredis-cli FLUSH§DB", "");
        rows.Add("F42 P6 on the same line as the DR§OP DATABASE", "tests/X/A.cs", $"await Run(\"DR§OP DATABASE bench_case\"); // {Scoped(Owner)}", "");
        rows.Add("F42 P7 DR§OP DATABASE of any name is clean under the marker: the guard reads a spelling, not whose database it is", "a.sh", $"# {Scoped(Owner)}\npsql -c 'DR§OP DATABASE production_db'", "");
        rows.Add("F42 N1 no owner after the spelling", "tests/X/A.cs", $"// {Scoped("the per-test databases live and die with")}\nvar a = 1;", "exemption");
        rows.Add("F42 N2 a placeholder reason", "tests/X/A.cs", $"// {Scoped("todo")}\nvar a = 1;", "exemption");
        rows.Add("F42 N3 no reason", "tests/X/A.cs", $"// {Marker} container-scoped\nvar a = 1;", "exemption");
        rows.Add("F42 N4 neither spelling", "tests/X/A.cs", $"// {Scoped("the per-test databases are removed by the PostgresFixture")}\nvar a = 1;", "exemption");
        rows.Add("F42 N4 a half spelling", "tests/X/A.cs", $"// {Scoped("the databases die with the PostgresFixture container")}\nvar a = 1;", "exemption");
        rows.Add("F42 N5 over TRUN§CATE", "a.sh", $"# {Scoped(Owner)}\npsql -c 'TRUN§CATE TABLE users'", "shared-store");
        rows.Add("F42 N6 over FLUSH§ALL", "a.sh", $"# {Scoped(Owner)}\nredis-cli FLUSH§ALL", "shared-store");
        rows.Add("F42 N7 over deleteMany of everything", ".github/scripts/a.mjs", $"// {Scoped(Owner)}\nawait users.delete§Many({{}});", "shared-store");
        rows.Add("F42 N8 over DR§OP TABLE", "a.sh", $"# {Scoped(Owner)}\npsql -c 'DR§OP TABLE users'", "shared-store");
        rows.Add("F42 N9 two lines above the DR§OP DATABASE", "a.sh", $"# {Scoped(Owner)}\necho between\npsql -c 'DR§OP DATABASE bench_case'", "shared-store");
        rows.AddCSharpOnly("F42 N10 over a container started without the remove flag", "a.sh", $"# {Scoped(Owner)}\ndocker r§un alpine", "docker-run", "both guards find docker-run, but they word it differently: this repository has no container carve-out (rule 5) and the C# guard says so in its header");
        rows.Add("F42 N11 over a container named outside the owner form", "a.sh", $"# {Scoped(Owner)}\ndocker r§un --rm --na§me web alpine", "docker-name");
        rows.Add("F42 N12 over the reaper switched off", ".github/workflows/w.yml", $"        # {Scoped(Owner)}\n        TESTCONTAINERS_RYUK_DISABLED: \"true\"", "ryuk-off");
        rows.Add("F42 N13 over a sweep by the bare prefix", "a.sh", $"# {Scoped(Owner)}\nrm -rf /t§mp/{P}-*", "owner-blind");
        rows.Add("F42 N14 over a raw temp path", ".github/scripts/a.mjs", $"// {Scoped(Owner)}\nconst d = fs.mkd§tempSync('x-');", "raw-temp");
        rows.Add("F42 N15 the shared-store exemption without a lock is still an exemption finding", "a.sh", $"-- {Marker} shared-store — pg:5432 users; reuse intentional\npsql -c 'TRUN§CATE TABLE users'", "exemption,shared-store");
        rows.Add("F42 N16 an article and nothing after it", "tests/X/A.cs", $"// {Scoped("the per-test databases lives and dies with the")}\nvar a = 1;", "exemption");
        rows.Add("F42 N16 an article twice", "tests/X/A.cs", $"// {Scoped("the per-test databases lives and dies with the the")}\nvar a = 1;", "exemption");
        rows.Add("F42 N16 a one-character word is no owner", "tests/X/A.cs", $"// {Scoped("the per-test databases lives and dies with the X")}\nvar a = 1;", "exemption");
        rows.Add("F42 N16 the dash of the reason is no owner", "tests/X/A.cs", $"// {Scoped("the per-test databases lives and dies with — the end")}\nvar a = 1;", "exemption");
        rows.Add("F42 N17 over a git work§tree add", "a.sh", $"# {Scoped(Owner)}\ngit work§tree add x HEAD", "raw-worktree");
        rows.AddCSharpOnly("F42 N18 over a legacy-named literal", "tests/X/A.cs", $"// {Scoped(Owner)}\nvar n = \"{P}-agent-x-1\";", "legacy-name,owner-blind", "in a .cs file the C# guard also reads the bare prefix word as owner-blind (decision 3 of its header); the Node guard reads the name as legacy-name only");
        rows.Add("F42 N19 a teardown and a reset on one line: every hit must be a teardown", "a.sh", $"# {Scoped(Owner)}\npsql -c 'DR§OP DATABASE a; TRUN§CATE TABLE users'", "shared-store");
        rows.Add("F42 N19 a teardown and a DR§OP TABLE on one line", "a.sh", $"# {Scoped(Owner)}\npsql -c 'DR§OP DATABASE a; DR§OP TABLE users'", "shared-store");
        rows.Add("F42 N19 FLUSH§DB and FLUSH§ALL on one line", "a.sh", $"# {Scoped(Owner)}\nredis-cli FLUSH§DB; redis-cli FLUSH§ALL", "shared-store");
        rows.Add("F42 N19 teardowns only on one line", "a.sh", $"# {Scoped(Owner)}\npsql -c 'DR§OP DATABASE a; DR§OP SCHEMA b'; redis-cli FLUSH§DB", "");
    }

    /// <summary>The rows F41's tests hold for the owner corners and the teardown lines, and what Node answers for them.</summary>
    private static void ParityWithNode(Rows rows)
    {
        // Parity rows with the Node guard (F41's tests), item by item: what Node answers for the same text.
        rows.Add("F42 P1 an owner of two characters, one of them punctuation", "tests/X/A.cs", $"// {Scoped("the data lives and dies with x.")}\nvar a = 1;", "");
        rows.Add("F42 P1 an owner that starts with an underscore", "tests/X/A.cs", $"// {Scoped("the data lives and dies with the _x")}\nvar a = 1;", "");
        rows.Add("F42 P1 an underscore continues a word, so its_x is no article", "tests/X/A.cs", $"// {Scoped("the data lives and dies with its_x")}\nvar a = 1;", "");
        rows.Add("F42 P1 any whitespace between the words", "tests/X/A.cs", $"// {Scoped("the data lives and dies with the\tPostgresFixture")}\nvar a = 1;", "");
        rows.Add("F42 P1 the spelling twice, one occurrence with an owner is enough", "tests/X/A.cs", $"// {Scoped("the x lives and dies with the, and the y lives and dies with the Orders fixture")}\nvar a = 1;", "");
        rows.Add("F42 N16 a zero-width joiner before the spelling is a word character, so the spelling starts mid-word", "tests/X/A.cs", $"// {Scoped("\u200Dlives and dies with the Orders fixture")}\nvar a = 1;", "exemption");
        rows.Add("F42 N16 a letter before the spelling", "tests/X/A.cs", $"// {Scoped("xlives and dies with the fixture")}\nvar a = 1;", "exemption");
        rows.Add("F42 N16 the long s is not an s: the spelling is ASCII case classes, not a case fold", "tests/X/A.cs", $"// {Scoped("live\u017F and die\u017F with the fixture")}\nvar a = 1;", "exemption");
        rows.Add("F42 N16 a one-character word after an article", "tests/X/A.cs", $"// {Scoped("the per-test databases lives and dies with a b")}\nvar a = 1;", "exemption");
        rows.Add("F42 N16 an article alone, an", "tests/X/A.cs", $"// {Scoped("the per-test databases lives and dies with an")}\nvar a = 1;", "exemption");
        rows.Add("F42 N16 an article and a dot-word, a.b", "tests/X/A.cs", $"// {Scoped("the per-test databases lives and dies with a.b")}\nvar a = 1;", "exemption");
        rows.Add("F42 N16 an article followed by punctuation, a. foo", "tests/X/A.cs", $"// {Scoped("the per-test databases lives and dies with a. foo")}\nvar a = 1;", "exemption");
        rows.Add("F42 N16 an article followed by punctuation, an. foo", "tests/X/A.cs", $"// {Scoped("the per-test databases lives and dies with an. foo")}\nvar a = 1;", "exemption");
        rows.Add("F42 N16 an article joined to a word by a dash, the-fixture", "tests/X/A.cs", $"// {Scoped("the per-test databases lives and dies with the-fixture")}\nvar a = 1;", "exemption");
        rows.Add("F42 N16 an article and an article-led word, the the-fixture", "tests/X/A.cs", $"// {Scoped("the per-test databases lives and dies with the the-fixture")}\nvar a = 1;", "exemption");
        rows.Add("F42 P1 an then a zero-width joiner and a letter: the joiner is a word character, so an is no article there", "tests/X/A.cs", $"// {Scoped("the data lives and dies with an\u200Dx")}\nvar a = 1;", "");
        rows.Add("F42 N16 an article, a zero-width non-joiner and punctuation", "tests/X/A.cs", $"// {Scoped("the data lives and dies with a\u200C. foo")}\nvar a = 1;", "exemption");
        rows.Add("F42 N2 a placeholder repeated is no reason", "tests/X/A.cs", $"// {Scoped("n/a n/a n/a n/a")}\nvar a = 1;", "exemption");
        rows.Add("F42 N3 an empty reason after the dash", "tests/X/A.cs", $"// {Marker} container-scoped —\nvar a = 1;", "exemption");
        rows.Add("F42 N19 a teardown, a teardown and a delete-all", "a.sh", $"# {Scoped(Owner)}\nawait q('DR§OP DATABASE a; DR§OP SCHEMA b'); await users.delete§Many({{}});", "shared-store");
        // Node reads the FIRST match of each pattern for a finding: a second named drop after an owner-form first match is not reported,
        // marked or not; the marker only decides a line that already has a finding.
        rows.Add("F42 an owner-form drop then another drop on one line is clean, as in Node", "a.sh", $"psql -c 'DR§OP TABLE {Lower}_u1000_mcp_x_abc123; DR§OP TABLE users'", "");
        rows.Add("F42 an owner-form truncate then another truncate on one line is clean, as in Node", "a.sh", $"psql -c 'TRUN§CATE TABLE {Lower}_u1000_mcp_x_abc123; TRUN§CATE TABLE users'", "");
        rows.Add("F42 a teardown and a named drop of an owner-form name under the marker is clean: the owner form is no hit", "a.sh", $"# {Scoped(Owner)}\npsql -c 'DR§OP DATABASE bench; DR§OP TABLE {P}-u1-bench-x-1;'", "");
        rows.Add("F42 the same owner-form drop line under the marker is clean too, as in Node", "a.sh", $"# {Scoped(Owner)}\npsql -c 'DR§OP TABLE {Lower}_u1000_mcp_x_abc123; DR§OP TABLE users'", "");
        rows.Add("F42 an owner-form drop of a database then another database under the marker is clean, as in Node", "a.sh", $"# {Scoped(Owner)}\npsql -c 'DR§OP DATABASE {Lower}_u1000_mcp_x_abc123; DR§OP DATABASE b'", "");
        rows.Add("F42 the shared-store exemption (a) still clears a reset, as before", "a.sh", $"-- {Marker} shared-store — pg:5432 users; reuse intentional; lock: pg_advisory_lock(42)\npsql -c 'TRUN§CATE TABLE users'", "");
    }

    /// <summary>Shared-store lines with no marker: they must agree with the Node guard today, whatever the mounted release.</summary>
    private static void UnmarkedSharedStore(Rows rows)
    {
        rows.Add("TRUN§CATE TABLE", ".github/workflows/w.yml", "      - run: psql -c \"TRUN§CATE TABLE users\"", "shared-store");
        rows.Add("DR§OP DATABASE", "a.sh", "psql -c 'DR§OP DATABASE shop'", "shared-store");
        rows.Add("DR§OP TABLE of an owner-form name", "a.sh", $"psql -c 'DR§OP TABLE {P}-u1000-mcp-t-abc123'", "");
        rows.Add("FLUSH§ALL", "a.sh", "redis-cli FLUSH§ALL", "shared-store");
        rows.Add("deleteMany all", ".github/scripts/a.mjs", "await coll.delete§Many({});", "shared-store");
        rows.Add("the prose word truncated", "tests/X/A.cs", "var s = \"the output was truncated\";", "");
        rows.Add("a commented truncate", "tests/X/A.cs", "// TRUN§CATE TABLE users", "");
    }
}
