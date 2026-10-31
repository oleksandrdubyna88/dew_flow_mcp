using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Mcp.Tests.Leftovers;

/// <summary>One thing the guard refuses: the repository-relative path (forward slashes), the 1-based line, the class of rule §7
/// (<c>raw-temp</c> … <c>ryuk-off</c>, plus <c>exemption</c> and <c>token</c>, which are about the guard's own grammar) and the
/// text that was matched.</summary>
public sealed record Finding(string File, int Line, string Class, string Text)
{
    public override string ToString() => $"{File}:{Line} — {Class} — {Text}";
}

/// <summary>What a scan read and what it found. <see cref="FilesScanned"/> of zero is a broken discovery, never a clean
/// repository.</summary>
public sealed record GuardReport(int FilesScanned, IReadOnlyList<Finding> Findings)
{
    /// <summary>The failure message: every finding as <c>file:line — class — text</c>, or the one sentence for a scan of nothing.</summary>
    public string Describe(string root) => FilesScanned == 0
        ? $"scanned nothing under {root} — discovery broke; the repository is not clean"
        : string.Join(Environment.NewLine, Findings.Select(f => f.ToString()));
}

/// <summary>The structural guard of <c>common/test-leftovers.md</c> §7 over this repository's tests, source, scripts and CI: red on
/// every class below outside the helper, and red when it scanned nothing. A port of the SHAPE of the conventions' Node guard
/// (<c>tools/leftovers-check.mjs</c>) to this repository's languages — with differences it states rather than hides:
/// <list type="bullet">
/// <item><c>owner-blind</c> in C# is a LITERAL rule (owner decision 3): any literal of the bare prefix outside <c>Owned.cs</c>,
/// and any reference to <c>Owned.Prefix</c> or <c>Owned.LegacyPattern</c> outside this folder, is a finding, so a constant cannot
/// carry the prefix to a sweep. In scripts and workflows the class is judged by the SHIPPED classifier
/// (<c>tools/lib/sweeper-scan.mjs</c> of the mounted conventions) started through <c>node</c> — not a port, so the guard and the
/// machine scan can never disagree about a line.</item>
/// <item>the container carve-out of rule §5 is NOT recognised: this repository starts no container, and a container command without
/// the remove flag is a finding; a container that needs the carve-out re-opens this guard.</item>
/// <item>the docker classes read one line at a time; a command continued with a backslash is judged line by line.</item>
/// </list>
/// Every detector word below is spelled in two pieces, and the guard scans ITS OWN file clean: a test pins it.</summary>
public static class LeftoversGuard
{
    /// <summary>The files the scan never reads: the guard's own tests carry the violations as fixture strings. A test pins this list.</summary>
    public static IReadOnlyList<string> ExcludedFiles { get; } = ["tests/Mcp.Tests/Leftovers/LeftoversGuardTests.cs"];

    private const string TokenFile = "tests/Mcp.Tests/Leftovers/OwnedToken.cs";
    private const string HelperFile = "tests/Mcp.Tests/Leftovers/Owned.cs";
    private const string HelperFolder = "tests/Mcp.Tests/Leftovers/";
    private const string RulePath = "common/test-leftovers.md";
    private const string ClassifierPath = ".claude/rules/shared/tools/lib/sweeper-scan.mjs";

    // The detector words, each in two pieces: the guard is scanned like any other file, and the machine scan reads it too.
    private const string Word = "teststo" + "delete";
    private const string MarkerWord = "testsToDe" + "lete-exempt:";

    private static readonly string[] NeverEntered = ["bin", "obj", "node_modules", ".git", ".claude", ".http-artifacts", "logs"];

    private static readonly string[] Exemptible = ["raw-temp", "raw-worktree", "docker-run", "docker-name", "legacy-name", "shared-store"];

    private static Regex Rx(string pattern, RegexOptions options = RegexOptions.None) => new(pattern, options | RegexOptions.CultureInvariant);

    // ---- raw-temp, raw-worktree, ryuk-off, shared-store -------------------------------------------------------------------

    private static readonly Regex[] RawTemp =
    [
        Rx("Create" + "TempSubdirectory"),
        Rx("GetTemp" + "Path2?"),
        Rx("GetTemp" + "FileName"),
        Rx("GetEnvironment" + @"Variable\(\s*""(?:TEMP|TMP|TMPDIR)"""),
        Rx(@"\bmkd" + @"temp(?:Sync)?\s*\("),
        Rx(@"\btmp" + @"dir\s*\("),
        Rx(@"process\.env\." + "(?:TEMP|TMP|TMPDIR)" + @"\b"),
        Rx(@"\btemp" + @"_dir\s*\("),
    ];

    private static readonly Regex[] RawTempShell = [Rx(@"\bmk" + @"temp\b")];

    private static readonly Regex[] RawTempPowerShell = [Rx("New-Temporary" + @"File|\$env:TE?MP\b")];

    private static readonly Regex[] RawWorktree = [Rx(@"[""']work" + @"tree[""']\s*,\s*[""']add[""']"), Rx(@"\bwork" + @"tree\s+add\b")];

    private static readonly Regex RyukOff = Rx("TESTCONTAINERS_RYUK_" + @"DISABLED\s*[:=]\s*[""']?true", RegexOptions.IgnoreCase);

    private static readonly (Regex Pattern, bool Named)[] SharedStore =
    [
        (Rx(@"\bDR" + @"OP\s+(?:DATABASE|SCHEMA|TABLE)\s+(?:IF\s+EXISTS\s+)?([""'`]?[\w.$-]*)"), true),
        (Rx(@"\bTRUN" + @"CATE\s+(?:TABLE\s+)?([""'`]?[\w.$-]*)"), true),
        (Rx(@"\bFLUSH" + @"(?:ALL|DB)\b"), false),
        (Rx("delete" + @"Many\(\s*\{\s*\}\s*\)"), false),
    ];

    // ---- docker ---------------------------------------------------------------------------------------------------------

    private static readonly Regex DockerWord = Rx(@"\bdocker\b");
    private static readonly Regex ContainerStart = Rx(@"\bdocker\s+r" + @"un\b");
    private static readonly Regex HasRm = Rx(@"(?<![-\w])--rm\b");
    private static readonly Regex DashName = Rx(@"--na" + @"me[ =]+(?=\S)");
    private static readonly Regex CreateName = Rx(@"\bdocker\s+(volume|network)\s+create\b");
    private static readonly Regex WithName = Rx("With" + @"Name\(\s*""([^""]*)""");
    private static readonly Regex ImageBuild = Rx(@"\bdocker\s+build\b");
    private static readonly Regex TagFlag = Rx(@"(?<=\s)(?:-t|--tag)(?:\s+|=)(?=\S)");
    private static readonly Regex ComposeCommand = Rx(@"\bdocker(?:\s+|-)compose\b");
    private static readonly Regex ProjectFlag = Rx(@"(?<=\s)(?:-p|--project-name)(?:\s+|=)(?=\S)");
    private static readonly Regex ComposeEnv = Rx("COMPOSE_PROJECT" + @"_NAME[""']?\s*[:=]\s*[""']?");

    /// <summary>The OWNER form of a leftover name, one of the three spellings then an owner segment (rule §1, case-insensitive).</summary>
    private static readonly Regex OwnerForm = Rx("^" + Word + @"[-_][uw][0-9]{1,10}[-_]", RegexOptions.IgnoreCase);

    private static readonly Regex WholeVariable = Rx(@"^\$[A-Za-z_][A-Za-z0-9_]*\z");
    private static readonly Regex WholeBraced = Rx(@"^\$\{[A-Za-z_][A-Za-z0-9_]*(?::?[-=]([^${}]+))?\}\z");
    private static readonly Regex WholeSubstitution = Rx(@"^\$\(.*\)\z", RegexOptions.Singleline);

    // ---- legacy-name, owner-blind ------------------------------------------------------------------------------------------

    private static readonly Regex PrefixWord = Rx(Word + "[-_]", RegexOptions.IgnoreCase);
    private static readonly Regex OwnerSegmentAfter = Rx(@"^[uw][0-9]{1,10}[-_]", RegexOptions.IgnoreCase);
    private static readonly Regex SecondSegment = Rx("^[a-z0-9]+", RegexOptions.IgnoreCase);
    private static readonly Regex OwnerGrammar = Rx(@"^[uw][0-9]{1,10}\z", RegexOptions.IgnoreCase);
    private static readonly Regex EnvironmentName = Rx("^" + Word.ToUpperInvariant() + @"_[A-Z]+\z");
    private static readonly Regex BuiltFromToken = Rx(Word + @"[-_](?:\{\s*RepoToken\b|\$\{?\s*(?:REPO_TOKEN|RepoToken|repoToken)\b)", RegexOptions.IgnoreCase);
    private static readonly Regex HelperReference = Rx(@"\bOwned\.(?:Prefix|LegacyPattern)\b");

    // ---- exemptions ------------------------------------------------------------------------------------------------------

    private static readonly Regex Marker = Rx(@"(?://|#|--|/\*|\*|<!--)\s*" + Regex.Escape(MarkerWord) + "(.*)$");
    private static readonly Regex MarkerBody = Rx(@"^\s*([^\s—]*)\s*(?:—\s*(.*?))?\s*(?:\*/|-->)?\s*\z");
    private static readonly Regex Placeholder = Rx(@"^(?:see above|see below|needed|required|necessary|obvious|todo|tbd|n/?a|because|why|reason|it is needed)\b", RegexOptions.IgnoreCase);

    // ---- the token -------------------------------------------------------------------------------------------------------

    private static readonly Regex TokenShape = Rx(@"^[a-z0-9]{2,12}\z");
    private static readonly Regex TokenConstant = Rx(@"\bRepoToken\b\s*(?::\s*\w+\s*)?=\s*[""']([^""'\n]*)[""']");

    private enum Kind
    {
        Tests,
        Source,
        Workflow,
        Script,
    }

    private sealed record Source(string Relative, string Absolute, Kind Kind);

    /// <summary>Scan <paramref name="repoRoot"/>: <see cref="GuardReport"/> with the number of files read and every finding. Pure
    /// apart from reading files and, when the tree holds a script or a workflow, starting <paramref name="node"/> ONCE to run the
    /// mounted conventions' classifier (read from <paramref name="mountRoot"/>, default the scanned root). A root that does not
    /// exist scans nothing; it does not throw.</summary>
    public static GuardReport Scan(string repoRoot, string? mountRoot = null, string node = "node")
    {
        var sources = Discover(repoRoot).ToList();
        var findings = new List<Finding>();
        var judged = new List<Source>();
        foreach (var source in sources)
        {
            findings.AddRange(ScanText(source, File.ReadAllText(source.Absolute, Encoding.UTF8)));
            if (source.Kind is Kind.Script or Kind.Workflow) judged.Add(source);
        }

        if (judged.Count > 0) findings.AddRange(ClassifyScripts(judged, mountRoot ?? repoRoot, node));
        return new GuardReport(sources.Count, [.. findings.OrderBy(f => f.File, StringComparer.Ordinal).ThenBy(f => f.Line).ThenBy(f => f.Class, StringComparer.Ordinal)]);
    }

    // ---- discovery -------------------------------------------------------------------------------------------------------

    private static IEnumerable<Source> Discover(string root)
    {
        if (!Directory.Exists(root)) yield break;
        foreach (var path in Walk(root).Order(StringComparer.Ordinal))
        {
            var relative = Path.GetRelativePath(root, path).Replace('\\', '/');
            if (ExcludedFiles.Contains(relative)) continue;
            if (KindOf(relative) is { } kind) yield return new Source(relative, path, kind);
        }
    }

    /// <summary>Every file below <paramref name="dir"/>, except under a folder named <c>bin</c>, <c>obj</c>, <c>node_modules</c>,
    /// <c>.git</c>, <c>.claude</c> (the mounted conventions are another repository), <c>.http-artifacts</c> or <c>logs</c>. A link to
    /// a folder is not entered.</summary>
    private static IEnumerable<string> Walk(string dir)
    {
        foreach (var file in Directory.EnumerateFiles(dir)) yield return file;
        foreach (var sub in Directory.EnumerateDirectories(dir))
        {
            if (NeverEntered.Contains(Path.GetFileName(sub), StringComparer.Ordinal)) continue;
            if (new DirectoryInfo(sub).Attributes.HasFlag(FileAttributes.ReparsePoint)) continue;
            foreach (var file in Walk(sub)) yield return file;
        }
    }

    private static Kind? KindOf(string relative) => Path.GetExtension(relative).ToLowerInvariant() switch
    {
        ".cs" when relative.StartsWith("tests/", StringComparison.Ordinal) => Kind.Tests,
        ".cs" when relative.StartsWith("src/", StringComparison.Ordinal) => Kind.Source,
        ".yml" or ".yaml" when Regex.IsMatch(relative, @"^\.github/workflows/[^/]+\z") => Kind.Workflow,
        ".mjs" or ".js" or ".cjs" when relative.StartsWith(".github/scripts/", StringComparison.Ordinal) || relative.StartsWith("http/", StringComparison.Ordinal) => Kind.Script,
        ".sh" or ".ps1" or ".py" => Kind.Script,
        _ => null,
    };

    // ---- one file ----------------------------------------------------------------------------------------------------------

    private static List<Finding> ScanText(Source source, string text)
    {
        var extension = Path.GetExtension(source.Relative).ToLowerInvariant();
        var original = SplitLines(text);
        var matched = SplitLines(extension is ".cs" or ".mjs" or ".js" or ".cjs" ? StripBlockComments(text, csharp: extension == ".cs") : text);
        var exemptions = ReadExemptions(source.Relative, original, out var found);
        for (var i = 0; i < matched.Length; i++)
        {
            if (IsComment(matched[i])) continue;
            foreach (var group in LineHits(source, extension, matched[i]).GroupBy(h => h.Class))
            {
                var line = i + 1;
                if (group.Key != "ryuk-off" && IsExempt(exemptions, group.Key, line)) continue;
                found.Add(new Finding(source.Relative, line, group.Key, string.Join(", ", group.Select(h => $"`{h.Text}`").Distinct())));
            }
        }

        return found;
    }

    private static string[] SplitLines(string text) => [.. text.Split('\n').Select(l => l.EndsWith('\r') ? l[..^1] : l)];

    private static bool IsComment(string line)
    {
        var trimmed = line.TrimStart();
        return trimmed.StartsWith("//", StringComparison.Ordinal) || trimmed.StartsWith('#');
    }

    private static IEnumerable<(string Class, string Text)> LineHits(Source source, string extension, string line)
    {
        if (source.Kind != Kind.Source)
        {
            foreach (var hit in Matches(RawTemp, line)) yield return ("raw-temp", hit);
            if (extension is ".sh" or ".yml" or ".yaml") foreach (var hit in Matches(RawTempShell, line)) yield return ("raw-temp", hit);
            if (extension == ".ps1") foreach (var hit in Matches(RawTempPowerShell, line)) yield return ("raw-temp", hit);
            foreach (var hit in Matches(RawWorktree, line).Take(1)) yield return ("raw-worktree", hit);
            if (ContainerStart.IsMatch(line) && !HasRm.IsMatch(line)) yield return ("docker-run", "a container started without the remove flag");
            foreach (var hit in DockerNameHits(line)) yield return ("docker-name", hit);
            foreach (var hit in SharedStoreHits(line)) yield return ("shared-store", hit);
            if (RyukOff.IsMatch(line)) yield return ("ryuk-off", "the reaper is switched off");
        }

        foreach (var hit in LegacyNameHits(line)) yield return ("legacy-name", hit);
        if (source.Kind is Kind.Tests or Kind.Source)
        {
            foreach (var hit in OwnerBlindHits(source, line)) yield return ("owner-blind", hit);
        }
    }

    private static IEnumerable<string> Matches(IEnumerable<Regex> patterns, string line) =>
        patterns.Select(p => p.Match(line)).Where(m => m.Success).Select(m => m.Value.Trim());

    // ---- docker-name -----------------------------------------------------------------------------------------------------------

    private static IEnumerable<string> DockerNameHits(string line)
    {
        if (DockerWord.IsMatch(line))
        {
            foreach (Match m in DashName.Matches(line))
            {
                var name = ReadValue(line[(m.Index + m.Length)..]);
                if (!NameOk(name)) yield return $"the container name {name} is not an owner-form leftover name";
            }
        }

        foreach (Match m in CreateName.Matches(line))
        {
            var name = CreatedName(line[(m.Index + m.Length)..]);
            if (name.Length == 0) yield return $"docker {m.Groups[1].Value} create makes an unnamed {m.Groups[1].Value}";
            else if (!NameOk(name)) yield return $"docker {m.Groups[1].Value} create {name} is not an owner-form leftover name";
        }

        foreach (Match m in WithName.Matches(line))
        {
            if (!OwnerForm.IsMatch(m.Groups[1].Value)) yield return $"the fixture name \"{m.Groups[1].Value}\" is not an owner-form leftover name";
        }

        if (ImageBuild.IsMatch(line))
        {
            foreach (Match m in TagFlag.Matches(line))
            {
                var tag = ReadValue(line[(m.Index + m.Length)..]);
                if (!NameOk(tag)) yield return $"the image {tag} a test builds is not an owner-form leftover name";
            }
        }

        if (ComposeCommand.IsMatch(line))
        {
            foreach (Match m in ProjectFlag.Matches(line))
            {
                var name = ReadValue(line[(m.Index + m.Length)..]);
                if (!NameOk(name)) yield return $"the compose project {name} is not an owner-form leftover name";
            }
        }

        foreach (Match m in ComposeEnv.Matches(line))
        {
            var name = ReadValue(line[(m.Index + m.Length)..]);
            if (!NameOk(name)) yield return $"the compose project name is not an owner-form leftover name: {(name.Length == 0 ? "(empty)" : name)}";
        }
    }

    /// <summary>A name the guard accepts: the owner form, or a value that is wholly a shell expansion (<c>"${x}"</c>, <c>$(f a)</c>) —
    /// dynamic, never guessed at. A literal default (<c>${V:-w}</c>) is the name the command gets when V is unset, so it must be
    /// in the owner form.</summary>
    private static bool NameOk(string name)
    {
        if (OwnerForm.IsMatch(name)) return true;
        if (WholeVariable.IsMatch(name) || WholeSubstitution.IsMatch(name)) return true;
        var braced = WholeBraced.Match(name);
        return braced.Success && (!braced.Groups[1].Success || OwnerForm.IsMatch(braced.Groups[1].Value));
    }

    /// <summary>The value at the start of <paramref name="rest"/>: an opening quote is skipped, a <c>$(…)</c> or <c>${…}</c> is read
    /// whole (it may hold spaces), and the value ends at white space, a quote, a comma or a semicolon.</summary>
    private static string ReadValue(string rest)
    {
        var start = rest.Length > 0 && (rest[0] == '"' || rest[0] == '\'') ? 1 : 0;
        var i = start;
        while (i < rest.Length)
        {
            if (rest[i] == '$' && i + 1 < rest.Length && (rest[i + 1] == '(' || rest[i + 1] == '{'))
            {
                i = AfterClosing(rest, i + 1);
                continue;
            }

            if (char.IsWhiteSpace(rest[i]) || rest[i] is '"' or '\'' or ',' or ';') break;
            i++;
        }

        return rest[start..i];
    }

    private static int AfterClosing(string text, int open)
    {
        var up = text[open];
        var down = up == '(' ? ')' : '}';
        var depth = 0;
        for (var i = open; i < text.Length; i++)
        {
            if (text[i] == up) depth++;
            else if (text[i] == down && --depth == 0) return i + 1;
        }

        return text.Length;
    }

    /// <summary>The name argument of <c>docker volume|network create</c>: the first word that is not an option or an option's value.</summary>
    private static string CreatedName(string rest)
    {
        var words = rest.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        string[] withValue = ["-d", "--driver", "--label", "-l", "-o", "--opt", "--subnet", "--gateway", "--ip-range", "--scope", "--ipam-driver", "--na" + "me"];
        for (var i = 0; i < words.Length; i++)
        {
            if (!words[i].StartsWith('-')) return words[i].Trim('"', '\'', ',', ';');
            if (withValue.Contains(words[i], StringComparer.Ordinal)) i++;
        }

        return "";
    }

    // ---- shared-store ----------------------------------------------------------------------------------------------------------

    private static IEnumerable<string> SharedStoreHits(string line)
    {
        foreach (var (pattern, named) in SharedStore)
        {
            var m = pattern.Match(line);
            if (!m.Success) continue;
            if (named && OwnerForm.IsMatch(m.Groups[1].Value.TrimStart('"', '\'', '`'))) continue;
            yield return m.Value.Trim();
            yield break; // one hit per line, as the shipped guard counts
        }
    }

    // ---- legacy-name, owner-blind ------------------------------------------------------------------------------------------------

    /// <summary>Each literal of the prefix with its separator whose second segment is something other than an owner, an exempt marker
    /// word, the quarantine sibling's word or an environment name — and a prefix built from the repository token.</summary>
    private static IEnumerable<string> LegacyNameHits(string line)
    {
        foreach (Match m in PrefixWord.Matches(line))
        {
            var second = SecondSegment.Match(line[(m.Index + m.Length)..]);
            if (!second.Success || IsNotAName(m, second) || OwnerGrammar.IsMatch(second.Value)) continue;
            yield return m.Value + second.Value;
        }

        if (BuiltFromToken.IsMatch(line)) yield return "a prefix built from the repository token";
    }

    /// <summary>Decision 3, in C#: any literal of the bare prefix not followed by an owner segment is a finding, in every <c>.cs</c> file
    /// but the helper's own — and a reference to <c>Owned.Prefix</c> or <c>Owned.LegacyPattern</c> outside the helper's folder.</summary>
    private static IEnumerable<string> OwnerBlindHits(Source source, string line)
    {
        if (source.Relative != HelperFile)
        {
            foreach (Match m in PrefixWord.Matches(line))
            {
                var rest = line[(m.Index + m.Length)..];
                if (OwnerSegmentAfter.IsMatch(rest)) continue;
                var second = SecondSegment.Match(rest);
                if (second.Success && IsNotAName(m, second)) continue;
                yield return m.Value + (second.Success ? second.Value : "");
            }
        }

        if (!source.Relative.StartsWith(HelperFolder, StringComparison.Ordinal) && HelperReference.Match(line) is { Success: true } reference) yield return reference.Value;
    }

    /// <summary>The second segments that are not names: the exempt marker word, the quarantine sibling (<c>.&lt;name&gt;.&lt;prefix&gt;-quarantine-…</c>) and an
    /// environment variable (upper case, underscore).</summary>
    private static bool IsNotAName(Match prefix, Match second) =>
        second.Value.Equals("exempt", StringComparison.OrdinalIgnoreCase)
        || second.Value.Equals("quarantine", StringComparison.OrdinalIgnoreCase)
        || EnvironmentName.IsMatch(prefix.Value + second.Value);

    // ---- exemptions ------------------------------------------------------------------------------------------------------------------

    private static Dictionary<string, HashSet<int>> ReadExemptions(string file, string[] lines, out List<Finding> findings)
    {
        var valid = Exemptible.ToDictionary(c => c, _ => new HashSet<int>(), StringComparer.Ordinal);
        findings = [];
        for (var i = 0; i < lines.Length; i++)
        {
            var marker = Marker.Match(lines[i]);
            if (!marker.Success) continue;
            var (cls, problem) = Judge(marker.Groups[1].Value);
            if (cls is null) findings.Add(new Finding(file, i + 1, "exemption", problem));
            else valid[cls].Add(i + 1);
        }

        return valid;
    }

    /// <summary>The class a marker exempts, or why it exempts nothing.</summary>
    private static (string? Class, string Problem) Judge(string rest)
    {
        var parsed = MarkerBody.Match(rest);
        var cls = parsed.Groups[1].Value;
        var reason = parsed.Groups[2].Value;
        if (cls.Length == 0) return (null, "an exemption names no class");
        if (cls == "ryuk-off") return (null, "`ryuk-off` accepts no exemption");
        if (cls == "owner-blind") return (null, "`owner-blind` accepts no exemption of any kind");
        if (!Exemptible.Contains(cls)) return (null, $"an exemption names an unknown class `{cls}`");
        if (!IsReason(reason)) return (null, $"the exemption of `{cls}` carries no reason (ten characters, three words, not a placeholder)");
        if (cls == "shared-store" && !reason.Contains("lock:", StringComparison.Ordinal)) return (null, "the exemption of `shared-store` must name its lock (`lock: …`)");
        return (cls, "");
    }

    private static bool IsReason(string reason) =>
        reason.Length >= 10 && reason.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length >= 3 && !Placeholder.IsMatch(reason);

    private static bool IsExempt(Dictionary<string, HashSet<int>> exemptions, string cls, int line) =>
        exemptions.TryGetValue(cls, out var lines) && (lines.Contains(line) || lines.Contains(line - 1));

    // ---- block comments ----------------------------------------------------------------------------------------------------------------

    /// <summary>Each character inside a <c>/* … */</c> comment becomes a space and every line end is kept, so line numbers do not
    /// move. The walk respects string literals — a <c>/*</c> inside one opens nothing — and line comments.</summary>
    internal static string StripBlockComments(string text, bool csharp)
    {
        var sb = new StringBuilder(text.Length);
        var i = 0;
        while (i < text.Length)
        {
            var c = text[i];
            if (c == '/' && i + 1 < text.Length && text[i + 1] == '/')
            {
                var end = text.IndexOf('\n', i);
                end = end < 0 ? text.Length : end;
                sb.Append(text, i, end - i);
                i = end;
            }
            else if (c == '/' && i + 1 < text.Length && text[i + 1] == '*')
            {
                var close = text.IndexOf("*/", i + 2, StringComparison.Ordinal);
                var end = close < 0 ? text.Length : close + 2;
                for (var k = i; k < end; k++) sb.Append(text[k] is '\n' or '\r' ? text[k] : ' ');
                i = end;
            }
            else if (c is '"' or '\'' || (!csharp && c == '`'))
            {
                var end = EndOfLiteral(text, i, csharp);
                sb.Append(text, i, end - i);
                i = end;
            }
            else
            {
                sb.Append(c);
                i++;
            }
        }

        return sb.ToString();
    }

    /// <summary>The index just past the literal that starts at <paramref name="start"/>: a C# verbatim string (a doubled quote is a
    /// quote), a raw string, a regular string or char literal, or a JavaScript string or template.</summary>
    private static int EndOfLiteral(string text, int start, bool csharp)
    {
        var quote = text[start];
        if (csharp && quote == '"')
        {
            var verbatim = (start > 0 && text[start - 1] == '@') || (start > 1 && text[start - 1] == '$' && text[start - 2] == '@');
            if (verbatim)
            {
                for (var i = start + 1; i < text.Length; i++)
                {
                    if (text[i] != '"') continue;
                    if (i + 1 < text.Length && text[i + 1] == '"') i++;
                    else return i + 1;
                }

                return text.Length;
            }

            var run = 0;
            while (start + run < text.Length && text[start + run] == '"') run++;
            if (run >= 3)
            {
                var close = text.IndexOf(new string('"', run), start + run, StringComparison.Ordinal);
                return close < 0 ? text.Length : close + run;
            }

            if (run == 2) return start + 2; // the empty string
        }

        var multiline = !csharp && quote == '`';
        for (var i = start + 1; i < text.Length; i++)
        {
            if (text[i] == '\\') i++;
            else if (text[i] == quote) return i + 1;
            else if (text[i] == '\n' && !multiline) return i; // a regular literal ends with its line
        }

        return text.Length;
    }

    // ---- the shipped classifier --------------------------------------------------------------------------------------------------------

    /// <summary>The <c>owner-blind</c> class for scripts and workflows: the mounted conventions' own <c>classifyLines</c> (the one
    /// implementation the machine scan and the rule checks share), run through <c>node</c> once over every such file. A
    /// <c>broad</c> verdict on a line that is not a comment is a finding. <c>node</c> missing, the classifier missing, a non-zero exit
    /// or an unreadable answer is ONE finding that names the cause — the script half is never silently skipped.</summary>
    private static List<Finding> ClassifyScripts(List<Source> scripts, string mountRoot, string node)
    {
        var module = new Uri(Path.GetFullPath(Path.Combine(mountRoot, ClassifierPath))).AbsoluteUri;
        var code = string.Join('\n',
            "import { readFileSync } from \"node:fs\";",
            $"import {{ classifyLines }} from {JsonSerializer.Serialize(module)};",
            "for (const file of process.argv.slice(1)) {",
            "  classifyLines(readFileSync(file, \"utf8\")).forEach((verdict, index) => {",
            "    if (verdict === \"broad\") console.log(JSON.stringify({ file, line: index + 1 }));",
            "  });",
            "}");
        var start = new ProcessStartInfo(node) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        foreach (var argument in new[] { "--input-type=module", "-e", code, "--" }) start.ArgumentList.Add(argument);
        foreach (var script in scripts) start.ArgumentList.Add(script.Absolute);

        string stdout;
        string stderr;
        int exit;
        try
        {
            using var process = Process.Start(start) ?? throw new InvalidOperationException("the process did not start");
            var outTask = process.StandardOutput.ReadToEndAsync();
            var errTask = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(TimeSpan.FromMinutes(2)))
            {
                process.Kill(entireProcessTree: true);
                return [Unjudged(scripts, $"{node} did not finish in two minutes")];
            }

            process.WaitForExit();
            stdout = outTask.GetAwaiter().GetResult();
            stderr = errTask.GetAwaiter().GetResult();
            exit = process.ExitCode;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException)
        {
            return [Unjudged(scripts, $"{node} could not be started: {ex.Message}")];
        }

        if (exit != 0) return [Unjudged(scripts, $"{node} exited {exit}: {FirstLine(stderr)}")];
        var byPath = scripts.ToDictionary(s => s.Absolute, StringComparer.Ordinal);
        var found = new List<Finding>();
        foreach (var raw in stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            try
            {
                using var json = JsonDocument.Parse(raw);
                var script = byPath[json.RootElement.GetProperty("file").GetString()!];
                var line = json.RootElement.GetProperty("line").GetInt32();
                var lines = SplitLines(File.ReadAllText(script.Absolute, Encoding.UTF8));
                if (line - 1 < lines.Length && IsComment(lines[line - 1])) continue;
                found.Add(new Finding(script.Relative, line, "owner-blind", "matches the prefix with no owner segment after it (the shipped classifier)"));
            }
            catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
            {
                return [Unjudged(scripts, $"{node} printed a line that is not a verdict: {raw}")];
            }
        }

        return found;
    }

    private static Finding Unjudged(List<Source> scripts, string cause) =>
        new(scripts[0].Relative, 1, "owner-blind", $"the shipped classifier could not judge the scripts — {cause}");

    private static string FirstLine(string text) => text.Split('\n', 2)[0].TrimEnd('\r');

    // ---- the token ----------------------------------------------------------------------------------------------------------------------

    /// <summary>Findings (class <c>token</c>) when <paramref name="token"/> is not a row of the rule's <c>## Tokens</c> table, is of the
    /// owner grammar, or is another repository's row — <paramref name="identity"/> is the <c>repository</c> column of ITS row. Fails
    /// closed: a rule with no table registers nothing, and an empty identity proves nothing.</summary>
    public static IReadOnlyList<Finding> TokenFindings(string ruleText, string token, string identity)
    {
        if (!TokenShape.IsMatch(token)) return At(1, $"the token `{token}` is not [a-z0-9]{{2,12}} ({RulePath})");
        if (OwnerGrammar.IsMatch(token)) return At(1, $"the token `{token}` matches the owner grammar of {RulePath} §1 — no repository may be named like an owner");
        var table = ParseTokenTable(ruleText);
        if (table.Heading == 0) return At(1, $"{RulePath} has no `## Tokens` section — the token `{token}` is not registered");
        if (table.Header is null) return At(table.Heading, $"{RulePath} has no table under `## Tokens` — the token `{token}` is not registered");
        string[] expected = ["token", "family", "repository"];
        if (table.Header.Length != 3 || !expected.Select((name, i) => table.Header[i].Equals(name, StringComparison.OrdinalIgnoreCase)).All(ok => ok))
        {
            return At(table.Heading, $"the `## Tokens` table of {RulePath} is not `| token | family | repository |` — the token `{token}` is not registered");
        }

        var rows = table.Rows.Where(r => r.Token == token).ToList();
        if (rows.Count == 0) return At(table.Heading, $"the token `{token}` is not a row of the `## Tokens` table of {RulePath}");
        if (rows.Count > 1) return At(rows[1].Line, $"the token `{token}` is listed twice in {RulePath}");
        if (identity.Trim().Length == 0)
        {
            return At(rows[0].Line, $"this repository's identity is unknown, so the token `{token}` cannot be proven to belong to it — OwnedToken.RepoRow must be exactly the repository column of the row of `{token}` ({rows[0].Repository})");
        }

        return rows[0].Repository.Trim().Equals(identity.Trim(), StringComparison.OrdinalIgnoreCase)
            ? []
            : At(rows[0].Line, $"the token `{token}` belongs to `{rows[0].Repository}` in {RulePath}, not to this repository (`{identity}`)");
    }

    private static List<Finding> At(int line, string text) => [new Finding(RulePath, Math.Max(line, 1), "token", text)];

    /// <summary><see cref="TokenFindings"/> over the mounted rule, <c>.claude/rules/shared/common/test-leftovers.md</c> under
    /// <paramref name="repoRoot"/>. A missing file is a finding: an uninitialised submodule registers nothing.</summary>
    public static IReadOnlyList<Finding> RegisteredTokenFindings(string repoRoot, string token, string identity)
    {
        var rule = Path.Combine(repoRoot, ".claude", "rules", "shared", "common", "test-leftovers.md");
        return File.Exists(rule)
            ? TokenFindings(File.ReadAllText(rule), token, identity)
            : [new Finding(RulePath, 1, "token", $"{RulePath} is not mounted at {rule} — an uninitialised submodule registers nothing; run `git submodule update --init .claude/rules/shared`")];
    }

    /// <summary>The ONE constant (rule §2): over every <c>.cs</c>, <c>.mjs</c>, <c>.js</c>, <c>.cjs</c>, <c>.sh</c>, <c>.ps1</c> and
    /// <c>.py</c> file of the tree (the folders the scan never enters excepted), a non-comment line that declares <c>RepoToken</c> must
    /// occur exactly once — in the token file — and carry <paramref name="token"/>.</summary>
    public static IReadOnlyList<Finding> ConstantFindings(string repoRoot, string token)
    {
        if (!Directory.Exists(repoRoot)) return [new Finding(TokenFile, 1, "token", $"{repoRoot} does not exist — the token file cannot be read")];
        string[] extensions = [".cs", ".mjs", ".js", ".cjs", ".sh", ".ps1", ".py"];
        var found = new List<Finding>();
        var declared = 0;
        foreach (var path in Walk(repoRoot).Order(StringComparer.Ordinal))
        {
            var relative = Path.GetRelativePath(repoRoot, path).Replace('\\', '/');
            if (!extensions.Contains(Path.GetExtension(path).ToLowerInvariant()) || ExcludedFiles.Contains(relative)) continue;
            var lines = SplitLines(File.ReadAllText(path, Encoding.UTF8));
            for (var i = 0; i < lines.Length; i++)
            {
                if (IsComment(lines[i])) continue;
                foreach (Match m in TokenConstant.Matches(lines[i]))
                {
                    if (relative != TokenFile) found.Add(new Finding(relative, i + 1, "token", $"RepoToken is declared here, outside {TokenFile} — the token is written once (rule §2)"));
                    else declared++;
                    if (m.Groups[1].Value != token) found.Add(new Finding(relative, i + 1, "token", $"RepoToken is `{m.Groups[1].Value}`, not the registered `{token}`"));
                }
            }
        }

        if (declared != 1) found.Add(new Finding(TokenFile, 1, "token", $"{TokenFile} must declare RepoToken exactly once; it declares it {declared} time(s)"));
        return found;
    }

    private sealed record TokenRow(string Token, string Repository, int Line);

    private sealed record TokenTable(int Heading, string[]? Header, List<TokenRow> Rows);

    private static TokenTable ParseTokenTable(string ruleText)
    {
        var lines = SplitLines(ruleText);
        var start = Array.FindIndex(lines, l => Regex.IsMatch(l, @"^##\s+Tokens\s*\z"));
        if (start < 0) return new TokenTable(0, null, []);
        var end = Array.FindIndex(lines, start + 1, l => Regex.IsMatch(l, @"^#{1,2}\s"));
        var section = lines[(start + 1)..(end < 0 ? lines.Length : end)];
        var headerAt = -1;
        for (var i = 0; i + 1 < section.Length && headerAt < 0; i++)
        {
            if (Regex.IsMatch(section[i], @"^\s*\|") && Regex.IsMatch(section[i + 1], @"^\s*\|?[\s:|-]+\|[\s:|-]*\z") && section[i + 1].Contains('-')) headerAt = i;
        }

        if (headerAt < 0) return new TokenTable(start + 1, null, []);
        var rows = new List<TokenRow>();
        for (var i = headerAt + 2; i < section.Length && Regex.IsMatch(section[i], @"^\s*\|"); i++)
        {
            var cells = Cells(section[i]);
            rows.Add(new TokenRow(Cell(cells, 0).Replace("`", ""), Cell(cells, 2), start + i + 2));
        }

        return new TokenTable(start + 1, Cells(section[headerAt]), rows);
    }

    private static string Cell(string[] cells, int index) => index < cells.Length ? cells[index] : "";

    private static string[] Cells(string row)
    {
        var trimmed = row.Trim();
        if (trimmed.StartsWith('|')) trimmed = trimmed[1..];
        if (trimmed.EndsWith('|')) trimmed = trimmed[..^1];
        return [.. trimmed.Split('|').Select(c => c.Trim())];
    }
}
