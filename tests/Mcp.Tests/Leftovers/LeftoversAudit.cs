namespace Mcp.Tests.Leftovers;

/// <summary>The end-of-run audit: a run that leaves a folder it claimed is a red run. It runs in the assembly fixture's
/// <c>DisposeAsync</c>, after every test of the assembly has finished, and catches the two leaks the guard cannot see — a helper
/// call nobody disposed, and a disposal that gave up. Two checks, one verdict:
/// <list type="bullet">
/// <item>the process-wide registry of <see cref="OwnedDir"/> (<see cref="Unreclaimed"/>): every folder created and never
/// disposed, AND every folder whose disposal was tried and failed, wherever its parent was;</item>
/// <item>the temp root (<see cref="ClaimedBy"/>): every depth-1 folder of this repository's pattern whose sibling claim is
/// exactly THIS run's record, and every such claim whose folder is gone. In an elevated Windows run the claim scan is blind
/// (the claims read as another owner's); the registry check is not.</item>
/// </list>
/// It takes only claims whose record is this process's own pid and start, and only when this process's folders carry that record
/// (no <c>TESTSTODELETE_RUN</c> handed down): a child of a run writes the RUN's record (rule §3), which a sibling that is still
/// working writes too, so scanning for it would take the sibling's folders. The registry already covers this process's own.</summary>
internal static class LeftoversAudit
{
    private const string ClaimSuffix = ".owner";

    /// <summary>Every folder of this run still registered: never disposed (<c>DisposeAttempted</c> false) and disposed but not
    /// removed (true).</summary>
    public static IReadOnlyList<(string Path, bool DisposeAttempted)> Unreclaimed() =>
        [.. OwnedDir.Unreclaimed().Select(d => (d.Path, d.DisposeAttempted))];

    /// <summary>Every depth-1 folder of <paramref name="tmp"/> whose name matches <see cref="Owned.Pattern"/> and whose sibling claim
    /// parses to exactly <paramref name="owner"/> (token, host, pid, start, namespace), plus the path of every such claim whose
    /// folder is missing. Nothing when this process writes another record than its own (<c>TESTSTODELETE_RUN</c>, read through
    /// <paramref name="env"/>): its folders are in the registry, and that record is a whole run's, not this process's.
    /// <paramref name="mine"/> is the kernel-owner seam of <see cref="Scope.ReadClaimText"/>.</summary>
    public static IReadOnlyList<string> ClaimedBy(string tmp, Owner owner, Func<string, bool>? mine = null, Func<string, string?>? env = null) =>
        [.. Discover(tmp, owner, mine, env).Select(c => c.Path)];

    /// <summary>What <see cref="ClaimedBy"/> finds, with what identifies each at the moment of discovery: a folder's own identity and
    /// its claim's (<see cref="Captured"/>), or an orphan claim's identity and text. The removal re-checks them.</summary>
    internal sealed record Claimed(string Path, LeftoversSweep.Captured? Folder, Identity? Claim, ClaimRead? Read);

    /// <param name="quarantinedNames">The seam that lists the sibling quarantines of <paramref name="tmp"/> (default <see cref="LeftoversSweep.QuarantinedNames"/>); called once.</param>
    internal static IReadOnlyList<Claimed> Discover(
        string tmp, Owner owner, Func<string, bool>? mine = null, Func<string, string?>? env = null, Func<string, HashSet<string>>? quarantinedNames = null,
        Func<string, IEnumerable<FileSystemInfo>>? listing = null)
    {
        if (Owner.RunRecord(env, _ => { }) != Owner.This || !RootExists(tmp)) return []; // a process that writes the run's record does not touch the root at all
        var pattern = Owned.Pattern();
        var quarantined = ListQuarantined(tmp, quarantinedNames);
        var found = new List<Claimed>();
        using var entries = (listing ?? Listing)(tmp).GetEnumerator(); // read one entry at a time, inside the error boundary: never the whole root first
        while (Advance(entries, tmp))
        {
            var entry = entries.Current;
            if (entry.Attributes.HasFlag(FileAttributes.ReparsePoint) || !pattern.IsMatch(entry.Name)) continue;
            if (Examine(entry, owner, mine, quarantined) is { } claimed) found.Add(claimed);
        }

        return found;
    }

    /// <summary>Is there a temp root at all? Read as the entry itself (<see cref="Native.IdentityOf"/>), not with <c>Directory.Exists</c>, which
    /// says false for an error exactly as it does for an absence: a root beneath a parent this process cannot traverse is not a missing
    /// root, and an audit that took it for one would pass a run that left a claimed folder in it. Only an entry that is not there (a path
    /// that runs through a file, a link to nothing included) is "missing"; one that cannot be reached is a <see cref="LeftoversAuditException"/>
    /// naming the root and the error. A link is judged by its FINAL target, read the same way (a target beneath a parent that cannot be traversed
    /// is unreachable, not missing).</summary>
    internal static bool RootExists(string tmp, Func<string, string?>? finalTarget = null, int depth = 0)
    {
        try
        {
            return Native.IdentityOf(tmp) is { } entry && (!entry.IsLink || LinkTargetExists(tmp, finalTarget, depth));
        }
        catch (IOException ex) when (IsNotADirectory(ex))
        {
            return false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException && ex is not LeftoversAuditException)
        {
            throw new LeftoversAuditException(tmp, ex);
        }
        catch (LeftoversAuditException ex) when (ex.InnerException is { } inner)
        {
            throw new LeftoversAuditException(tmp, inner); // named by the root the audit was given, the error says where it stopped
        }
    }

    /// <summary>A link is judged by its final target, one hop only: a target that is the link's own path, or that is not there to resolve, is an error (the link
    /// does not lead anywhere else), and a chain of links the resolver keeps handing on stops at <see cref="MaxLinkDepth"/>.</summary>
    private static bool LinkTargetExists(string link, Func<string, string?>? finalTarget, int depth)
    {
        if (depth >= MaxLinkDepth) throw new IOException($"more than {MaxLinkDepth} links in a row");
        var target = (finalTarget ?? FinalTarget)(link);
        if (target is null || SamePath(target, link)) throw new IOException("the link does not resolve to another path");
        return RootExists(target, finalTarget, depth + 1);
    }

    private const int MaxLinkDepth = 8;

    private static bool SamePath(string a, string b) =>
        string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private static string? FinalTarget(string link) => new DirectoryInfo(link).ResolveLinkTarget(returnFinalTarget: true)?.FullName;

    /// <summary>ENOTDIR: a component of the path is a file, so there is no such root (the native read's error carries the errno).</summary>
    private static bool IsNotADirectory(IOException error) =>
        !OperatingSystem.IsWindows() && error.InnerException is System.ComponentModel.Win32Exception native && native.NativeErrorCode == Errno.Current.Enotdir;

    private static IEnumerable<FileSystemInfo> Listing(string tmp) => new DirectoryInfo(tmp).EnumerateFileSystemInfos();

    /// <summary>The sibling quarantines of the root, listed ONCE per audit; a root that cannot be listed is a <see cref="LeftoversAuditException"/>.</summary>
    private static HashSet<string> ListQuarantined(string tmp, Func<string, HashSet<string>>? quarantinedNames)
    {
        try
        {
            return (quarantinedNames ?? LeftoversSweep.QuarantinedNames)(tmp);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new LeftoversAuditException(tmp, ex);
        }
    }

    /// <summary>The next entry of the root's listing. A failure while listing — at the first entry or halfway — is a
    /// <see cref="LeftoversAuditException"/> naming the root and the error, never an empty or a shortened answer.</summary>
    private static bool Advance(IEnumerator<FileSystemInfo> entries, string tmp)
    {
        try
        {
            return entries.MoveNext();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new LeftoversAuditException(tmp, ex);
        }
    }

    private static Claimed? Examine(FileSystemInfo entry, Owner owner, Func<string, bool>? mine, HashSet<string> quarantined) =>
        entry is DirectoryInfo folder ? OfFolder(folder.FullName, owner, mine) : OfOrphan(entry.FullName, owner, mine, quarantined);

    private static Claimed? OfFolder(string path, Owner owner, Func<string, bool>? mine) =>
        LeftoversSweep.Captured.Read(path, mine) is { } captured && ClaimIs(captured.Text, owner) ? new Claimed(path, captured, null, null) : null;

    /// <summary>A claim whose folder is missing: a regular file (never a link), with exactly the owner's record.</summary>
    private static Claimed? OfOrphan(string path, Owner owner, Func<string, bool>? mine, HashSet<string> quarantined)
    {
        if (!path.EndsWith(ClaimSuffix, StringComparison.Ordinal) || Directory.Exists(path[..^ClaimSuffix.Length])) return null;
        if (quarantined.Contains(Path.GetFileName(path[..^ClaimSuffix.Length]))) return null; // its folder is in a sibling quarantine
        if (Native.IdentityOf(path) is not { IsRegular: true, IsLink: false } written) return null;
        var read = Scope.ReadClaimText(path, mine);
        return ClaimIs(read, owner) ? new Claimed(path, null, written, read) : null;
    }

    private static bool ClaimIs(ClaimRead read, Owner owner) => read is ClaimRead.Text text && Owner.Parse(text.Value) == owner;

    /// <summary>The failure of a run that left something, or null when it left nothing. An empty group is omitted; a path both
    /// registered and found in the temp root is named once, under the registry's words.</summary>
    public static LeftoversAuditException? Verdict(IReadOnlyList<(string Path, bool DisposeAttempted)> unreclaimed, IReadOnlyList<string> claimed)
    {
        var registered = unreclaimed.Select(u => u.Path).ToHashSet(StringComparer.Ordinal);
        var never = unreclaimed.Where(u => !u.DisposeAttempted).Select(u => u.Path).ToList();
        var failed = unreclaimed.Where(u => u.DisposeAttempted).Select(u => u.Path).ToList();
        var onlyClaimed = claimed.Where(path => !registered.Contains(path)).ToList();
        return never.Count + failed.Count + onlyClaimed.Count == 0 ? null : new LeftoversAuditException(never, failed, onlyClaimed);
    }

    /// <summary>The audit: take the snapshot of <paramref name="registry"/>, dispose each of them once more (a handle released since
    /// lets this one succeed), then remove what <see cref="ClaimedBy"/> finds, and throw ONE exception (<see cref="LeftoversAuditException"/>) naming everything that was
    /// left — the folders are gone by then, so the next run starts clean and the red is this run's.</summary>
    public static void Run(string tmp, Owner owner, IReadOnlyList<OwnedDir> registry, Func<string, bool>? mine = null, Func<string, string?>? env = null, SweepHooks? hooks = null)
    {
        var left = registry.Select(d => (d.Path, d.DisposeAttempted)).ToList();
        foreach (var dir in registry) dir.Dispose();
        var claimed = DiscoverKeeping(left, tmp, owner, mine, env);
        foreach (var item in claimed) Remove(item, mine, hooks);
        if (Verdict(left, [.. claimed.Select(c => c.Path)]) is { } failure) throw failure;
    }

    /// <summary>The discovery of <see cref="Run"/>: a root that cannot be reached does not lose what the registry held — the failure names the
    /// folders never disposed and disposed but not removed too.</summary>
    private static IReadOnlyList<Claimed> DiscoverKeeping(List<(string Path, bool DisposeAttempted)> left, string tmp, Owner owner, Func<string, bool>? mine, Func<string, string?>? env)
    {
        try
        {
            return Discover(tmp, owner, mine, env);
        }
        catch (LeftoversAuditException unreachable) when (unreachable.UnreadableRoot is { } root && unreachable.InnerException is { } error)
        {
            throw new LeftoversAuditException(root, error, [.. left.Where(l => !l.DisposeAttempted).Select(l => l.Path)], [.. left.Where(l => l.DisposeAttempted).Select(l => l.Path)]);
        }
    }

    /// <summary>The sweep's own removal (<see cref="LeftoversSweep.RemoveCaptured"/>), by the identity captured at discovery: a
    /// folder or claim that changed since is kept and said, never removed on the strength of an earlier look.</summary>
    private static void Remove(Claimed item, Func<string, bool>? mine, SweepHooks? hooks)
    {
        var result = item.Folder is { } folder
            ? LeftoversSweep.RemoveCaptured(item.Path, folder, mine, hooks)
            : LeftoversSweep.RemoveCapturedClaim(item.Path, item.Claim!.Value, item.Read!, mine);
        foreach (var kept in result.Preserved) Owned.DefaultWarn($"audit: kept {kept.Path} — {kept.Reason}");
        foreach (var error in result.Errors) Owned.DefaultWarn("audit: " + error);
    }
}
