using System.Text.RegularExpressions;

namespace Mcp.Tests.Leftovers;

/// <summary>The exemption marker of rule §7: a comment <c>&lt;prefix&gt;-exempt: &lt;kind&gt; — &lt;reason&gt;</c> on the line of a finding or the line
/// above it. A marker is itself judged: its kind must be one the rule knows, the reason must be a reason (ten characters, three words, not
/// a placeholder), a <c>shared-store</c> marker must name its lock, and a <c>container-scoped</c> marker must name what it lives and dies with.
/// A marker that is valid and has nothing beside it is clean: it is a declaration (the rule's §6 (b) is one), not a finding, so nothing here
/// reports a marker for exempting nothing. A marker that is refused is a finding of class <c>exemption</c>, and it exempts nothing.
/// <para>Two kinds. A kind of <see cref="Exemptible"/> clears a finding of the SAME class on its line and the next.
/// <see cref="ContainerScoped"/> is a declaration of its own (rule §6 (b)): it clears ONE class, <c>shared-store</c>, and only for the
/// container's own teardown (<c>DROP DATABASE</c>, <c>DROP SCHEMA</c>, <c>FLUSHDB</c>; the guard asks for it through <see cref="IsExempt"/>
/// with <see cref="ContainerScoped"/>); it never clears <c>docker-run</c>, <c>docker-name</c>, <c>raw-temp</c>, <c>raw-worktree</c>,
/// <c>legacy-name</c>, <c>owner-blind</c> or <c>ryuk-off</c>. What the guard cannot know: that the database dropped under it is the
/// container's — it reads a spelling, not data flow, so <c>DROP DATABASE production_db</c> under a valid marker is clean too. The marker is
/// the author's declaration, read in review; the lifetime is proved by the fixture's own test.</para></summary>
internal static class GuardExemptions
{
    /// <summary>The classes a marker may exempt. <c>ryuk-off</c> and <c>owner-blind</c> accept none.</summary>
    internal static readonly string[] Exemptible = ["raw-temp", "raw-worktree", "docker-run", "docker-name", "legacy-name", "shared-store"];

    /// <summary>The kind of marker rule §6 (b) prescribes for a database, schema or namespace inside a disposable container the run or its
    /// job owns. Not a class of finding, so it is not in <see cref="Exemptible"/> (that list keeps its meaning: the classes a marker exempts).</summary>
    internal const string ContainerScoped = "container-scoped";

    private const string MarkerWord = LeftoversGuard.CamelWord + "-exempt:";

    private static readonly Dictionary<string, string> Refused = new(StringComparer.Ordinal)
    {
        [""] = "an exemption names no class",
        ["ryuk-off"] = "`ryuk-off` accepts no exemption",
        ["owner-blind"] = "`owner-blind` accepts no exemption of any kind",
    };

    private static readonly Regex Marker = Rx(@"(?://|#|--|/\*|\*|<!--)\s*" + Regex.Escape(MarkerWord) + "(.*)$");
    private static readonly Regex MarkerBody = Rx(@"^\s*([^\s—]*)\s*(?:—\s*(.*?))?\s*(?:\*/|-->)?\s*\z");
    private static readonly Regex Placeholder = Rx(@"^(?:see above|see below|needed|required|necessary|obvious|todo|tbd|n/?a|because|why|reason|it is needed)\b", RegexOptions.IgnoreCase);

    /// <summary>What a <c>container-scoped</c> reason names: <c>live and die with</c> or <c>lives and dies with</c>, then an optional article
    /// (<c>the</c>, <c>a</c>, <c>an</c>, <c>its</c>, <c>this</c>, <c>that</c>) and an owner — a word of at least two characters that is not itself an article.
    /// "… lives and dies with the" names no owner.</summary>
    private static readonly Regex NamedOwner = Rx(
        @"\b(?:lives\s+and\s+dies|live\s+and\s+die)\s+with\s+(?:(?:the|a|an|its|this|that)\s+)?(?!(?:the|a|an|its|this|that)\b)[\p{L}\p{N}_][\p{L}\p{N}_.-]+",
        RegexOptions.IgnoreCase);

    private static Regex Rx(string pattern, RegexOptions options = RegexOptions.None) => new(pattern, options | RegexOptions.CultureInvariant);

    /// <summary>The lines each kind is valid on (every <see cref="Exemptible"/> class and <see cref="ContainerScoped"/>), and a finding for every
    /// marker that is refused.</summary>
    internal static Dictionary<string, HashSet<int>> Read(string file, string[] lines, out List<Finding> findings)
    {
        var valid = Exemptible.Append(ContainerScoped).ToDictionary(c => c, _ => new HashSet<int>(), StringComparer.Ordinal);
        var markers = lines.Select((line, i) => (Marker: Marker.Match(line), Number: i + 1)).Where(m => m.Marker.Success)
            .Select(m => (m.Number, Verdict: Judge(m.Marker.Groups[1].Value))).ToList();
        foreach (var (number, verdict) in markers.Where(m => m.Verdict.Class is not null)) valid[verdict.Class!].Add(number);
        findings = [.. markers.Where(m => m.Verdict.Class is null).Select(m => new Finding(file, m.Number, "exemption", m.Verdict.Problem))];
        return valid;
    }

    /// <summary>Is <paramref name="cls"/> (a class, or <see cref="ContainerScoped"/>) exempt on <paramref name="line"/> — a valid marker of that kind on the line or the one above?</summary>
    internal static bool IsExempt(Dictionary<string, HashSet<int>> exemptions, string cls, int line) =>
        exemptions.TryGetValue(cls, out var lines) && (lines.Contains(line) || lines.Contains(line - 1));

    /// <summary>The kind a marker declares, or why it exempts nothing.</summary>
    private static (string? Class, string Problem) Judge(string rest)
    {
        var parsed = MarkerBody.Match(rest);
        var cls = parsed.Groups[1].Value;
        var problem = ClassProblem(cls) ?? ReasonProblem(cls, parsed.Groups[2].Value);
        return problem is null ? (cls, "") : (null, problem);
    }

    private static string? ClassProblem(string cls) =>
        Refused.TryGetValue(cls, out var refusal) ? refusal : Exemptible.Contains(cls) || cls == ContainerScoped ? null : $"an exemption names an unknown class `{cls}`";

    private static string? ReasonProblem(string cls, string reason) =>
        !IsReason(reason) ? $"the exemption of `{cls}` carries no reason (ten characters, three words, not a placeholder)" : KindProblem(cls, reason);

    private static string? KindProblem(string cls, string reason) =>
        cls == "shared-store" ? LockProblem(reason) : cls == ContainerScoped ? OwnerProblem(reason) : null;

    private static string? LockProblem(string reason) =>
        reason.Contains("lock:", StringComparison.Ordinal) ? null : "the exemption of `shared-store` must name its lock (`lock: …`)";

    private static string? OwnerProblem(string reason) =>
        NamedOwner.IsMatch(reason) ? null : "the exemption of `container-scoped` must name what it lives and dies with (`<what> lives and dies with <fixture or job>`)";

    private static bool IsReason(string reason) =>
        reason.Length >= 10 && reason.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length >= 3 && !Placeholder.IsMatch(reason);
}
