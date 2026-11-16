namespace Mcp.Tests.Leftovers;

/// <summary>The end-of-run audit's verdict, typed: this run left folders it claimed. The lists are what a caller reads; the message is
/// what a person (and the CI step's <c>grep</c>) reads. Its FIRST line is <c>mcp tests: this run left &lt;n&gt; folder(s) it claimed — …</c>
/// (CI matches it); then one folder per line under the words of its group; then one line saying what to do.</summary>
public sealed class LeftoversAuditException : InvalidOperationException
{
    public LeftoversAuditException(IReadOnlyList<string> neverDisposed, IReadOnlyList<string> disposedNotRemoved, IReadOnlyList<string> claimedInTempRoot)
        : base(MessageFor(neverDisposed, disposedNotRemoved, claimedInTempRoot))
    {
        NeverDisposed = neverDisposed;
        DisposedNotRemoved = disposedNotRemoved;
        ClaimedInTempRoot = claimedInTempRoot;
    }

    /// <summary>The audit could not reach <paramref name="root"/>, so it cannot say what this run left in it: the run is red, not clean. The
    /// lists are empty; the message names the root and the error.</summary>
    public LeftoversAuditException(string root, Exception error)
        : this(root, error, [], [])
    {
    }

    /// <summary>The same, and what the registry held at that moment: the folders never disposed and disposed but not removed are named first,
    /// under their groups, as in any other verdict.</summary>
    public LeftoversAuditException(string root, Exception error, IReadOnlyList<string> neverDisposed, IReadOnlyList<string> disposedNotRemoved)
        : base(RootMessage(root, error, neverDisposed, disposedNotRemoved), error)
    {
        UnreadableRoot = root;
        NeverDisposed = neverDisposed;
        DisposedNotRemoved = disposedNotRemoved;
        ClaimedInTempRoot = [];
    }

    /// <summary>The temp root the audit could not reach, when that is what this failure says.</summary>
    public string? UnreadableRoot { get; }

    private static string RootMessage(string root, Exception error, IReadOnlyList<string> never, IReadOnlyList<string> failed)
    {
        var sentence = $"{OwnedToken.RepoToken} tests: the end-of-run audit could not read the temp root {root} ({error.GetType().Name}: {error.Message}) — it cannot say what this run left there; remedy: make the folder (and every parent of it) reachable by this user again, or point TMPDIR at one that is";
        return never.Count + failed.Count == 0 ? sentence : MessageFor(never, failed, []) + "\n" + sentence;
    }

    /// <summary>Folders created through the helper and never disposed.</summary>
    public IReadOnlyList<string> NeverDisposed { get; }

    /// <summary>Folders whose disposal was tried and gave up.</summary>
    public IReadOnlyList<string> DisposedNotRemoved { get; }

    /// <summary>Folders (and orphan claims) found in the temp root that claim this process and are in neither list above.</summary>
    public IReadOnlyList<string> ClaimedInTempRoot { get; }

    private static string MessageFor(IReadOnlyList<string> never, IReadOnlyList<string> failed, IReadOnlyList<string> claimed)
    {
        var groups = new[] { ("never disposed", never), ("disposed but not removed", failed), ("claimed in the temp root", claimed) }.Where(g => g.Item2.Count > 0).ToList();
        var count = groups.Sum(g => g.Item2.Count);
        var summary = string.Join(", ", groups.Select(g => $"{g.Item2.Count} {g.Item1}"));
        var folders = groups.SelectMany(g => g.Item2.Select(path => $"  {g.Item1}: {path}"));
        return string.Join('\n', new[] { $"{OwnedToken.RepoToken} tests: this run left {count} folder(s) it claimed — {summary}" }.Concat(folders).Append(Remedy));
    }

    private static string Remedy =>
        $"remedy: dispose the OwnedDir or OwnedScope that made each (a test class field, disposed by xUnit); list what is left with: bash .agents/conventions/tools/janitor.sh --token {OwnedToken.RepoToken} --dry-run";
}
