using FluentAssertions;
using Xunit;

namespace Mcp.Tests.Leftovers;

/// <summary>The guard of rule §7 over this repository's tests, scripts and CI (S4 scenarios 3-6). THIS FILE IS EXCLUDED FROM
/// THE SCAN — it carries the violations as fixture strings — and it is the only one: a test pins the list. The prefix word
/// is still assembled from pieces at run time, because a literal here would travel into every copy of this file. Every
/// tree is written at run time into a private <see cref="OwnedDir"/>; the scripts in it are judged by the SHIPPED classifier
/// through the real mount, so each expected count is what the mount's own scan prints for the same text.</summary>
public sealed class LeftoversGuardTests : IDisposable
{
    private static readonly string P = "testsTo" + "Delete";
    private static readonly string Lower = P.ToLowerInvariant();
    private static readonly string Marker = P + "-exempt:";

    private readonly OwnedScope _owned = new();

    public void Dispose() => _owned.Dispose();

    // ---- 3: break-it per class ----------------------------------------------------------------------------------------

    public static TheoryData<string, string, string, string> BreakIt()
    {
        var d = new TheoryData<string, string, string, string>();
        void Add(string id, string file, string text, string expected) => d.Add(id, file, text, expected);

        // raw-temp
        Add("cs create-temp-subdirectory", "tests/X/A.cs", "var d = Directory.CreateTempSubdirectory(\"x\");", "raw-temp");
        Add("cs get-temp-path without a call", "tests/X/A.cs", "var f = Path.GetTempPath;", "raw-temp");
        Add("cs get-temp-path2", "tests/X/A.cs", "var f = Path.GetTempPath2();", "raw-temp");
        Add("cs get-temp-file-name", "tests/X/A.cs", "var f = Path.GetTempFileName();", "raw-temp");
        Add("cs env TMPDIR", "tests/X/A.cs", "var t = Environment.GetEnvironmentVariable(\"TMPDIR\");", "raw-temp");
        Add("cs env TEMP", "tests/X/A.cs", "var t = Environment.GetEnvironmentVariable( \"TEMP\" );", "raw-temp");
        Add("cs RUNNER_TEMP is not one", "tests/X/A.cs", "var t = Environment.GetEnvironmentVariable(\"RUNNER_TEMP\");", "");
        Add("sh mktemp", "a.sh", "d=$(mktemp -d)", "raw-temp");
        Add("workflow mktemp", ".github/workflows/w.yml", "      - run: d=$(mktemp -d)", "raw-temp");
        Add("ps1 $env:TEMP", "b.ps1", "$d = $env:TEMP", "raw-temp");
        Add("ps1 New-TemporaryFile", "b.ps1", "$f = New-TemporaryFile", "raw-temp");
        Add("mjs mkdtempSync", ".github/scripts/a.mjs", "const d = fs.mkdtempSync(path.join(base, 'x-'));", "raw-temp");
        Add("mjs tmpdir", ".github/scripts/a.mjs", "const d = os.tmpdir();", "raw-temp");
        Add("mjs process.env.TMPDIR", ".github/scripts/a.mjs", "const d = process.env.TMPDIR;", "raw-temp");
        Add("py mkdtemp", "c.py", "d = tempfile.mkdtemp()", "raw-temp");
        Add("py temp_dir", "c.py", "d = temp_dir()", "raw-temp");
        Add("sh mktemp is not a cs word", "tests/X/A.cs", "var s = \"mktemp\";", "");

        // raw-worktree
        Add("cs worktree add", "tests/X/A.cs", "Run(\"git\", \"worktree\", \"add\", dir);", "raw-worktree");
        Add("sh git worktree add", "a.sh", "git worktree add x", "raw-worktree");

        // docker-run and docker-name
        Add("docker run without --rm", ".github/workflows/w.yml", "      - run: docker run -d img", "docker-run");
        Add("docker run --rm owner-form name", ".github/workflows/w.yml", $"      - run: docker run --rm --name {P}-u1000-mcp-x-abc123 img", "");
        Add("docker run --rm unowned name", ".github/workflows/w.yml", "      - run: docker run --rm --name cache img", "docker-name");
        Add("docker run --rm legacy name", ".github/workflows/w.yml", $"      - run: docker run --rm --name {P}-mcp-x-abc123 img", "docker-name,legacy-name");
        Add("docker name is a whole expansion", "a.sh", "docker run --rm --name \"${name}\" img", "");
        Add("docker name is a command substitution", "a.sh", "docker run --rm --name \"$(mk name)\" img", "");
        Add("docker name default that is not an owner form", "a.sh", "docker run --rm --name \"${NAME:-cache}\" img", "docker-name");
        Add("docker volume create", "a.sh", "docker volume create cache", "docker-name");
        Add("docker network create with an option", "a.sh", "docker network create --driver bridge cache", "docker-name");
        Add("docker volume create owner form", "a.sh", $"docker volume create {P}-u1000-mcp-v-abc123", "");
        Add("cs WithName", "tests/X/A.cs", "var b = new Builder().WithName(\"pg\");", "docker-name");
        Add("cs WithName owner form", "tests/X/A.cs", $"var b = new Builder().WithName(\"{P}-u1000-mcp-pg-abc123\");", "");
        Add("docker compose -p", "a.sh", "docker compose -p cache up", "docker-name");
        Add("docker compose --project-name", "a.sh", "docker compose --project-name=cache up", "docker-name");
        Add("COMPOSE_PROJECT_NAME", ".github/workflows/w.yml", "        COMPOSE_PROJECT_NAME: cache", "docker-name");
        Add("docker build -t", "a.sh", "docker build -t cache .", "docker-name");

        // shared-store, ryuk-off
        Add("TRUNCATE TABLE", ".github/workflows/w.yml", "      - run: psql -c \"TRUNCATE TABLE users\"", "shared-store");
        Add("DROP DATABASE", "a.sh", "psql -c 'DROP DATABASE shop'", "shared-store");
        Add("DROP TABLE of an owner-form name", "a.sh", $"psql -c 'DROP TABLE {P}-u1000-mcp-t-abc123'", "");
        Add("FLUSHALL", "a.sh", "redis-cli FLUSHALL", "shared-store");
        Add("deleteMany all", ".github/scripts/a.mjs", "await coll.deleteMany({});", "shared-store");
        Add("the prose word truncated", "tests/X/A.cs", "var s = \"the output was truncated\";", "");
        Add("a commented truncate", "tests/X/A.cs", "// TRUNCATE TABLE users", "");
        Add("ryuk off", ".github/workflows/w.yml", "        TESTCONTAINERS_RYUK_DISABLED: \"true\"", "ryuk-off");
        Add("ryuk on", ".github/workflows/w.yml", "        TESTCONTAINERS_RYUK_DISABLED: \"false\"", "");

        // exemptions
        Add("exempt raw-temp", "tests/X/A.cs", $"// {Marker} raw-temp — the one place a test learns the temp root\nvar t = Path.GetTempPath();", "");
        Add("exempt raw-temp on the same line", "tests/X/A.cs", $"var t = Path.GetTempPath(); // {Marker} raw-temp — the one place a test learns the temp root", "");
        Add("exempt raw-temp in a block comment", "tests/X/A.cs", $"/* {Marker} raw-temp — the one place a test learns the temp root */\nvar t = Path.GetTempPath();", "");
        Add("exempt two lines above does not reach", "tests/X/A.cs", $"// {Marker} raw-temp — the one place a test learns the temp root\n\nvar t = Path.GetTempPath();", "raw-temp");
        Add("exempt for another class does not reach", "tests/X/A.cs", $"// {Marker} raw-worktree — the one place a test learns the temp root\nvar t = Path.GetTempPath();", "raw-temp");
        Add("exempt with a placeholder reason", "tests/X/A.cs", $"// {Marker} raw-temp — needed\nvar t = Path.GetTempPath();", "exemption,raw-temp");
        Add("exempt with a todo reason", "tests/X/A.cs", $"// {Marker} raw-temp — todo fix this later on\nvar t = Path.GetTempPath();", "exemption,raw-temp");
        Add("exempt with two words", "tests/X/A.cs", $"// {Marker} raw-temp — only twowords\nvar t = Path.GetTempPath();", "exemption,raw-temp");
        Add("exempt with an unknown class", "tests/X/A.cs", $"// {Marker} nonsense — a perfectly fine reason here\nvar t = Path.GetTempPath();", "exemption,raw-temp");
        Add("exempt with no class", "tests/X/A.cs", $"// {Marker}\nvar t = Path.GetTempPath();", "exemption,raw-temp");
        Add("exempt shared-store with a lock", "a.sh", $"-- {Marker} shared-store — pg:5432 users; reuse intentional; lock: pg_advisory_lock(42)\npsql -c 'TRUNCATE TABLE users'", "");
        Add("exempt shared-store without a lock", "a.sh", $"-- {Marker} shared-store — pg:5432 users; reuse intentional\npsql -c 'TRUNCATE TABLE users'", "exemption,shared-store");
        Add("ryuk-off under another class's exemption", ".github/workflows/w.yml", $"        # {Marker} shared-store — pg:5432 users; reuse intentional; lock: pg_advisory_lock(42)\n        TESTCONTAINERS_RYUK_DISABLED: \"true\"", "ryuk-off");
        Add("ryuk-off under its own exemption", ".github/workflows/w.yml", $"        # {Marker} ryuk-off — the reaper is slow on this runner\n        TESTCONTAINERS_RYUK_DISABLED: \"true\"", "exemption,ryuk-off");

        // comments
        Add("a commented-out raw-temp", "tests/X/A.cs", "// Directory.CreateTempSubdirectory(\"x\")", "");
        Add("a doc-commented raw-temp", "tests/X/A.cs", "/// Directory.CreateTempSubdirectory(\"x\")", "");
        Add("a shell comment", "a.sh", "# mktemp -d", "");
        Add("a block comment on one line", "tests/X/A.cs", "/* Path.GetTempPath() */", "");
        Add("a block comment over three lines", "tests/X/A.cs", "/*\n Path.GetTempPath()\n*/", "");
        Add("code after a block comment closes", "tests/X/A.cs", "/* x */ var t = Path.GetTempPath();", "raw-temp");
        Add("a slash-star inside a string opens nothing", "tests/X/A.cs", "var glob = \"src/**/*.cs\"; var t = Path.GetTempPath();", "raw-temp");
        Add("a slash-star inside a verbatim string opens nothing", "tests/X/A.cs", "var s = @\"a \"\"/*\"\" b\"; var t = Path.GetTempPath();", "raw-temp");
        Add("a slash-star inside a char literal pair opens nothing", "tests/X/A.cs", "var c = '/'; var d = '*'; var t = Path.GetTempPath();", "raw-temp");
        Add("a quote char literal opens no string", "tests/X/A.cs", "var q = '\"'; /* x */ var t = Path.GetTempPath();", "raw-temp");
        Add("a slash-star inside a line comment opens nothing", "tests/X/A.cs", "// see /* here\nvar t = Path.GetTempPath();", "raw-temp");
        Add("a block comment in mjs", ".github/scripts/a.mjs", "/* os.tmpdir() */", "");
        Add("a template string holding a slash-star in mjs", ".github/scripts/a.mjs", "const g = `src/**/*.cs`; const d = os.tmpdir();", "raw-temp");

        // legacy-name, owner-blind in .cs (decision 3)
        Add("legacy literal", "tests/X/A.cs", $"var n = \"{P}-mcp-x-abc123\";", "legacy-name,owner-blind");
        Add("legacy literal exempted", "tests/X/A.cs", $"// {Marker} legacy-name — a fixture of the form before the owner\nvar n = \"{P}-mcp-x-abc123\";", "owner-blind");
        Add("a prefix built from the token", "tests/X/A.cs", $"var n = $\"{P}-{{RepoToken}}-\";", "legacy-name,owner-blind");
        Add("the exempt marker word", "tests/X/A.cs", $"var n = \"{P}-exempt:\";", "");
        Add("a quarantine sibling name", "tests/X/A.cs", $"var n = \".x.{Lower}-quarantine-1-ab\";", "");
        Add("the owner environment variable", "tests/X/A.cs", "var n = \"TESTSTODELETE_OWNER\";", "");
        Add("a bare prefix with a dash", "tests/X/A.cs", $"var n = \"{P}-\";", "owner-blind");
        Add("a bare lower-case prefix with an underscore", "tests/X/A.cs", $"var n = \"{Lower}_\";", "owner-blind");
        Add("an owner-form literal", "tests/X/A.cs", $"var n = \"{P}-u1000-\";", "");
        Add("an owner-form literal of the windows form", "tests/X/A.cs", $"var n = \"{P}-w4294967295-\";", "");
        Add("a bare prefix concatenated", "tests/X/A.cs", $"var n = \"{P}-\" + x;", "owner-blind");
        Add("a constant carrying the prefix", "tests/X/A.cs", $"private const string P = \"{P}-\";", "owner-blind");
        Add("the bare prefix in the helper file", "tests/Mcp.Tests/Leftovers/Owned.cs", $"public const string Prefix = \"{P}-\";", "");
        Add("the bare prefix in src", "src/Y/Y.cs", $"var n = \"{P}-\";", "owner-blind");
        Add("the bare prefix, no exemption of any kind", "tests/X/A.cs", $"// {Marker} owner-blind — any reason at all here\nvar n = \"{P}-\";", "exemption,owner-blind");
        Add("a reference to Owned.Prefix outside the helper", "tests/X/A.cs", "var p = Owned.Prefix + what;", "owner-blind");
        Add("a reference to Owned.Prefix in the helper's folder", "tests/Mcp.Tests/Leftovers/X.cs", "var p = Owned.Prefix + what;", "");
        Add("a reference to Owned.LegacyPattern in src", "src/Y/Y.cs", "var m = Owned.LegacyPattern.IsMatch(n);", "owner-blind");
        Add("a commented reference to Owned.Prefix", "tests/X/A.cs", "// var p = Owned.Prefix + what;", "");
        Add("a reference to Owned.NameBase is how a test builds a name", "tests/X/A.cs", "var p = Owned.NameBase + what;", "");
        Add("src is read for the literal classes only", "src/Y/Y.cs", "var d = Directory.CreateTempSubdirectory(\"x\");", "");

        // owner-blind in .cs: the word itself, whatever follows it (a glob, a class, a pattern, a bare word)
        Add("a directory listing by the bare word and a star", "tests/X/A.cs", $"foreach (var d in Directory.GetDirectories(Owned.TempRoot(), \"{P}*\")) Directory.Delete(d, true);", "owner-blind,raw-temp");
        Add("a pattern over the bare word with a question mark", "tests/X/A.cs", $"var hits = Directory.GetDirectories(root, \"{P}?\");", "owner-blind");
        Add("a regex over the word and a class separator", "tests/X/A.cs", $"var r = new Regex(\"^{Lower}[-_]mcp[-_]\");", "owner-blind");
        Add("the word and its separator in two literals", "tests/X/A.cs", $"var n = \"{P}\" + \"-\";", "owner-blind");
        Add("the word interpolated and a separator after it", "tests/X/A.cs", "var n = $\"{\"" + P + "\"}-\";", "owner-blind");
        Add("the bare word alone", "tests/X/A.cs", $"var n = \"{P}\";", "owner-blind");
        Add("the bare word in a verbatim string", "tests/X/A.cs", $"var n = @\"{Lower}*\";", "owner-blind");
        Add("an owner-form glob", "tests/X/A.cs", $"var hits = Directory.GetDirectories(root, \"{P}-u1000-*\");", "");
        Add("an owner-form regex", "tests/X/A.cs", $"var r = new Regex(\"^{Lower}[-_]u1000[-_]\");", "");
        Add("the owner environment variable in a call", "tests/X/A.cs", "var v = Environment.GetEnvironmentVariable(\"TESTSTODELETE_RUN\");", "");
        Add("a lower-case environment name is not the variable", "tests/X/A.cs", $"var v = \"{Lower}_run\";", "legacy-name,owner-blind");
        Add("the bare word in a script is the classifier's, not the C# rule's", "a.sh", $"echo {P}", "");

        // Owned.TempRoot() is the temp root: outside the helper's folder it is a raw-temp finding like any other spelling of it
        Add("a folder made under Owned.TempRoot outside the helper", "tests/X/A.cs", "Directory.CreateDirectory(Path.Combine(Owned.TempRoot(), \"x\"));", "raw-temp");
        Add("Owned.TempRoot in src", "src/Y/Y.cs", "var root = Owned.TempRoot();", "raw-temp");
        Add("Owned.TempRoot in the helper's folder", "tests/Mcp.Tests/Leftovers/X.cs", "var root = Owned.TempRoot();", "");
        Add("a read-only use of Owned.TempRoot, exempted with a reason", "tests/X/A.cs", $"// {Marker} raw-temp — a read-only use, nothing is made there\nvar root = Owned.TempRoot();", "");
        Add("a commented Owned.TempRoot", "tests/X/A.cs", "// var root = Owned.TempRoot();", "");

        // scripts, judged by the shipped classifier through node
        Add("sh rm -rf glob", "a.sh", $"rm -rf \"$TMPDIR\"/{P}-*", "owner-blind");
        Add("sh find -name class", "a.sh", $"find /tmp -name '{Lower}[-_]*' -delete", "owner-blind");
        Add("sh a bare u glob", "a.sh", $"rm -rf /tmp/{P}-u*", "legacy-name,owner-blind");
        Add("sh a quoted owner reads as broad: the quote is not an owner start (the plan expected 0)", "a.sh", $"rm -rf /tmp/{P}-\"$OWNER\"-*", "owner-blind");
        Add("sh a bare variable owner", "a.sh", $"rm -rf /tmp/{P}-$OWNER-*", "");
        Add("sh a braced owner", "a.sh", $"rm -rf /tmp/{P}-${{OWNER}}-*", "");
        Add("sh a literal first segment that no owner can start with", "a.sh", $"rm -rf /tmp/{P}-conventions-*", "legacy-name");
        Add("sh a variable held on another line", "a.sh", $"P='{P}-'\necho x\nrm -rf \"$P\"*", "owner-blind");
        Add("mjs a const held on another line and removed", ".github/scripts/a.mjs", $"const P = \"{P}-\";\nfor (const n of names) fs.rmSync(P + n);", "owner-blind");
        Add("mjs a glob built by concatenation is a documented limit of the shipped tracer", ".github/scripts/a.mjs", $"const P = \"{P}-\";\nconst hits = glob(P + \"*\");", "");
        Add("ps1 a variable held on another line", "b.ps1", $"$P = \"{P}-\"\nRemove-Item \"$P*\"", "owner-blind");
        Add("py a variable held on another line and removed", "c.py", $"P = \"{P}-\"\nfor n in names: shutil.rmtree(P + n)", "owner-blind");
        Add("py a glob built by concatenation is a documented limit of the shipped tracer", "c.py", $"P = \"{P}-\"\nglob.glob(P + \"*\")", "");
        Add("workflow run block", ".github/workflows/w.yml", $"      - run: rm -rf /tmp/{P}-*", "owner-blind");
        Add("a name, not a pattern", "a.sh", $"echo \"{P}-u1000-mcp-x-abc123\"", "");
        Add("a script comment is not read", "a.sh", $"# rm -rf /tmp/{P}-*", "");

        return d;
    }

    [Theory]
    [MemberData(nameof(BreakIt))]
    public void Each_class_is_found_where_it_is_written_and_nowhere_else(string id, string file, string text, string expected)
    {
        var root = Tree((file, text));

        var report = LeftoversGuard.Scan(root, RepositoryRoot.Path);

        report.FilesScanned.Should().Be(1, id);
        Classes(report).Should().Be(expected, $"{id}: {Show(report)}");
    }

    [Fact]
    public void A_finding_names_the_file_the_line_and_the_class()
    {
        var root = Tree(("tests/X/A.cs", "var a = 1;\nvar b = 2;\nvar t = Path.GetTempPath();\n"));

        var finding = LeftoversGuard.Scan(root, RepositoryRoot.Path).Findings.Should().ContainSingle().Subject;

        finding.File.Should().Be("tests/X/A.cs");
        finding.Line.Should().Be(3);
        finding.Class.Should().Be("raw-temp");
        finding.Text.Should().Contain("GetTempPath");
    }

    [Fact]
    public void Code_after_a_block_comment_that_closes_on_its_line_is_found_at_that_line()
    {
        var root = Tree(("tests/X/A.cs", "/*\n x\n*/ var t = Path.GetTempPath();\n"));

        LeftoversGuard.Scan(root, RepositoryRoot.Path).Findings.Should().ContainSingle().Which.Line.Should().Be(3);
    }

    [Fact]
    public void A_script_finding_is_reported_at_the_use_not_at_the_assignment()
    {
        var root = Tree(("a.sh", $"P='{P}-'\necho x\nrm -rf \"$P\"*\n"));

        LeftoversGuard.Scan(root, RepositoryRoot.Path).Findings.Should().ContainSingle().Which.Line.Should().Be(3);
    }

    [Fact]
    public void A_line_that_breaks_a_class_twice_is_one_finding_of_that_class()
    {
        var root = Tree(("tests/X/A.cs", "var a = Path.GetTempPath() + Path.GetTempFileName();"));

        LeftoversGuard.Scan(root, RepositoryRoot.Path).Findings.Should().ContainSingle().Which.Class.Should().Be("raw-temp");
    }

    [Fact]
    public void A_file_under_a_build_output_is_not_scanned_and_the_folders_that_are_not_ours_are_never_entered()
    {
        var violation = "var t = Path.GetTempPath();";
        var root = Tree(
            ("tests/X/A.cs", "var ok = 1;"),
            ("tests/X/bin/Debug/B.cs", violation),
            ("tests/X/obj/C.cs", violation),
            ("http/node_modules/pkg/index.mjs", "const d = os.tmpdir();"),
            (".git/hooks/x.sh", "d=$(mktemp -d)"),
            (".claude/rules/shared/tools/x.sh", "d=$(mktemp -d)"),
            (".http-artifacts/x.sh", "d=$(mktemp -d)"),
            ("logs/x.sh", "d=$(mktemp -d)"));

        var report = LeftoversGuard.Scan(root, RepositoryRoot.Path);

        report.FilesScanned.Should().Be(1);
        report.Findings.Should().BeEmpty();
    }

    [Fact]
    public void Discovery_reads_the_tests_the_source_the_workflows_and_every_script()
    {
        var root = Tree(
            ("tests/X/A.cs", "var a = 1;"),
            ("src/Y/B.cs", "var b = 1;"),
            (".github/workflows/w.yml", "name: w"),
            (".github/workflows/v.yaml", "name: v"),
            (".github/scripts/s.mjs", "const a = 1;"),
            ("http/h.mjs", "const a = 1;"),
            ("tools/t.sh", "echo t"),
            ("tools/u.ps1", "Write-Host u"),
            ("tools/v.py", "print('v')"),
            ("docs/not-scanned.cs", "var t = Path.GetTempPath();"),
            ("README.md", "d=$(mktemp -d)"));

        var report = LeftoversGuard.Scan(root, RepositoryRoot.Path);

        report.FilesScanned.Should().Be(9);
        report.Findings.Should().BeEmpty();
    }

    [Fact]
    public void The_guard_over_itself_reports_nothing_because_it_spells_its_detector_words_in_pieces()
    {
        var path = "tests/Mcp.Tests/Leftovers/LeftoversGuard.cs";
        var root = Tree((path, File.ReadAllText(Path.Combine(RepositoryRoot.Path, path))));

        var report = LeftoversGuard.Scan(root, RepositoryRoot.Path);

        report.FilesScanned.Should().Be(1);
        report.Findings.Should().BeEmpty(Show(report));
    }

    [Fact]
    public void Without_node_the_script_half_is_a_finding_never_a_silence()
    {
        var root = Tree(("a.sh", "echo hello"));
        var missing = "no-such-node-" + Guid.NewGuid().ToString("N");

        var report = LeftoversGuard.Scan(root, RepositoryRoot.Path, missing);

        var finding = report.Findings.Should().ContainSingle().Subject;
        finding.Class.Should().Be("owner-blind");
        finding.Text.Should().Contain(missing).And.Contain("shipped classifier");
    }

    [Fact]
    public void Without_the_mounted_classifier_the_script_half_is_a_finding_naming_the_cause()
    {
        var root = Tree(("a.sh", "echo hello"));
        var bare = _owned.Dir("bare-mount");

        var report = LeftoversGuard.Scan(root, bare);

        report.Findings.Should().ContainSingle().Which.Class.Should().Be("owner-blind");
        report.Findings[0].Text.Should().Contain("shipped classifier");
    }

    [Fact]
    public void A_tree_with_no_script_never_starts_node()
    {
        var root = Tree(("tests/X/A.cs", "var a = 1;"));

        var report = LeftoversGuard.Scan(root, mountRoot: null, node: "no-such-node-" + Guid.NewGuid().ToString("N"));

        report.Findings.Should().BeEmpty();
    }

    // ---- 4: a scan of nothing is red -----------------------------------------------------------------------------------

    [Fact]
    public void A_scan_of_nothing_says_so_instead_of_reporting_clean()
    {
        var empty = _owned.Dir("empty-tree");
        var missing = Path.Combine(_owned.Dir("parent"), "does-not-exist");

        var nothing = LeftoversGuard.Scan(empty, RepositoryRoot.Path);
        var absent = LeftoversGuard.Scan(missing, RepositoryRoot.Path);

        nothing.FilesScanned.Should().Be(0);
        nothing.Findings.Should().BeEmpty();
        absent.FilesScanned.Should().Be(0);
        absent.Findings.Should().BeEmpty();
        nothing.Describe(empty).Should().Contain("scanned nothing under " + empty).And.Contain("discovery broke; the repository is not clean");
    }

    // ---- 5: the token is registered and is ours ------------------------------------------------------------------------

    private const string Heading = "## Tokens\n\n";
    private const string Header = "| token | family | repository |\n|---|---|---|\n";

    private static string Rule(string rows, string header = Header) => "# rule\n\n" + Heading + header + rows + "\n\n## Next\n";

    private const string McpRow = "| `mcp` | dew_flow | the MCP server |";

    [Fact]
    public void The_real_token_is_a_row_of_the_mounted_rule_and_is_declared_once_in_the_token_file()
    {
        LeftoversGuard.RegisteredTokenFindings(RepositoryRoot.Path, OwnedToken.RepoToken, OwnedToken.RepoRow).Should().BeEmpty();
        LeftoversGuard.ConstantFindings(RepositoryRoot.Path, OwnedToken.RepoToken).Should().BeEmpty();
    }

    [Theory]
    [InlineData("nosuchtoken")]
    [InlineData("Mcp")]
    [InlineData("x")]
    [InlineData("u1000")]
    [InlineData("w1")]
    public void A_token_that_is_not_registered_or_not_of_the_shape_is_one_finding(string token)
    {
        LeftoversGuard.TokenFindings(Rule(McpRow), token, "the MCP server").Should().ContainSingle().Which.Class.Should().Be("token");
    }

    [Fact]
    public void The_registered_token_of_this_repository_is_clean()
    {
        LeftoversGuard.TokenFindings(Rule(McpRow), "mcp", "the MCP server").Should().BeEmpty();
        LeftoversGuard.TokenFindings(Rule(McpRow), "mcp", "THE MCP SERVER ").Should().BeEmpty("the identity is trimmed and case-insensitive");
    }

    [Fact]
    public void A_rule_without_a_tokens_table_registers_nothing()
    {
        LeftoversGuard.TokenFindings("# rule\n\nno table here\n", "mcp", "the MCP server").Should().ContainSingle();
        LeftoversGuard.TokenFindings(Rule(McpRow, "| token | family |\n|---|---|\n"), "mcp", "the MCP server").Should().ContainSingle();
        LeftoversGuard.TokenFindings("## Tokens\n\nprose only\n", "mcp", "the MCP server").Should().ContainSingle();
    }

    [Fact]
    public void A_token_listed_twice_is_one_finding()
    {
        LeftoversGuard.TokenFindings(Rule(McpRow + "\n" + McpRow), "mcp", "the MCP server").Should().ContainSingle().Which.Text.Should().Contain("listed twice");
    }

    [Fact]
    public void A_token_that_appears_only_in_the_repository_column_is_not_registered()
    {
        var rule = Rule("| `other` | dew_flow | the mcp thing |");

        LeftoversGuard.TokenFindings(rule, "mcp", "the MCP server").Should().ContainSingle();
    }

    [Fact]
    public void A_token_that_belongs_to_another_repository_is_one_finding_naming_both()
    {
        var rule = Rule("| `mcp` | dew_flow | the embedding sidecar |");

        var finding = LeftoversGuard.TokenFindings(rule, "mcp", "the MCP server").Should().ContainSingle().Subject;

        finding.Text.Should().Contain("the embedding sidecar").And.Contain("the MCP server");
    }

    [Fact]
    public void An_empty_identity_fails_closed()
    {
        LeftoversGuard.TokenFindings(Rule(McpRow), "mcp", "").Should().ContainSingle();
        LeftoversGuard.TokenFindings(Rule(McpRow), "mcp", "   ").Should().ContainSingle();
    }

    [Fact]
    public void A_missing_rule_file_is_a_finding_because_an_uninitialised_submodule_registers_nothing()
    {
        var root = _owned.Dir("no-mount");

        var found = LeftoversGuard.RegisteredTokenFindings(root, "mcp", "the MCP server");

        found.Should().ContainSingle().Which.Text.Should().Contain("git submodule update --init");
    }

    private const string TokenFile = "tests/Mcp.Tests/Leftovers/OwnedToken.cs";

    [Fact]
    public void The_token_constant_is_written_once_in_the_token_file_with_the_registered_value()
    {
        var one = Tree((TokenFile, "public const string RepoToken = \"mcp\";"));
        var twice = Tree((TokenFile, "public const string RepoToken = \"mcp\";"), ("tests/Other.cs", "const string RepoToken = \"mcp\";"));
        var elsewhere = Tree(("src/Other.cs", "const string RepoToken = \"mcp\";"));
        var other = Tree((TokenFile, "public const string RepoToken = \"mcpx\";"));
        var commented = Tree((TokenFile, "public const string RepoToken = \"mcp\";"), ("tests/Other.cs", "// const string RepoToken = \"mcp\";"));
        var script = Tree((TokenFile, "public const string RepoToken = \"mcp\";"), ("a.sh", "RepoToken='mcp'"));

        var none = Tree(("tests/X/A.cs", "var a = 1;"));

        LeftoversGuard.ConstantFindings(one, "mcp").Should().BeEmpty();
        LeftoversGuard.ConstantFindings(none, "mcp").Should().ContainSingle().Which.Text.Should().Contain("exactly once");
        LeftoversGuard.ConstantFindings(twice, "mcp").Should().ContainSingle().Which.Class.Should().Be("token");
        LeftoversGuard.ConstantFindings(elsewhere, "mcp").Should().NotBeEmpty("not declared in the token file");
        LeftoversGuard.ConstantFindings(other, "mcp").Should().ContainSingle().Which.Text.Should().Contain("mcpx");
        LeftoversGuard.ConstantFindings(commented, "mcp").Should().BeEmpty("a comment declares nothing");
        LeftoversGuard.ConstantFindings(script, "mcp").Should().ContainSingle("a script's second declaration counts too");
    }

    // ---- 6: the exclusion list -----------------------------------------------------------------------------------------

    [Fact]
    public void The_exclusion_list_is_exactly_the_guards_own_fixtures_and_a_file_by_that_name_is_not_read()
    {
        LeftoversGuard.ExcludedFiles.Should().Equal("tests/Mcp.Tests/Leftovers/LeftoversGuardTests.cs");

        var root = Tree(
            ("tests/Mcp.Tests/Leftovers/LeftoversGuardTests.cs", "var t = Path.GetTempPath();"),
            ("tests/Mcp.Tests/Leftovers/LeftoversGuard.cs", "var ok = 1;"));

        var report = LeftoversGuard.Scan(root, RepositoryRoot.Path);

        report.FilesScanned.Should().Be(1);
        report.Findings.Should().BeEmpty();
    }

    // ---- helpers -------------------------------------------------------------------------------------------------------

    private string Tree(params (string Path, string Text)[] files)
    {
        var root = _owned.Dir("guard-tree");
        foreach (var (relative, text) in files)
        {
            var full = Path.Combine(root, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, text);
        }

        return root;
    }

    private static string Classes(GuardReport report) => string.Join(",", report.Findings.Select(f => f.Class).Order(StringComparer.Ordinal));

    private static string Show(GuardReport report) => string.Join("; ", report.Findings.Select(f => $"{f.File}:{f.Line} — {f.Class} — {f.Text}"));
}
