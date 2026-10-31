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
/// It takes only claims whose record is this process's own pid and start, so it can never remove another live test's folder.</summary>
internal static class LeftoversAudit
{
    private const string ClaimSuffix = ".owner";

    /// <summary>Every folder of this run still registered: never disposed (<c>DisposeAttempted</c> false) and disposed but not
    /// removed (true).</summary>
    public static IReadOnlyList<(string Path, bool DisposeAttempted)> Unreclaimed() =>
        [.. OwnedDir.Unreclaimed().Select(d => (d.Path, d.DisposeAttempted))];

    /// <summary>Every depth-1 folder of <paramref name="tmp"/> whose name matches <see cref="Owned.Pattern"/> and whose sibling claim
    /// parses to exactly <paramref name="owner"/> (token, host, pid, start, namespace), plus the path of every such claim whose
    /// folder is missing. <paramref name="mine"/> is the kernel-owner seam of <see cref="Scope.ReadClaimText"/>.</summary>
    public static IReadOnlyList<string> ClaimedBy(string tmp, Owner owner, Func<string, bool>? mine = null)
    {
        var found = new List<string>();
        if (!Directory.Exists(tmp)) return found;
        var pattern = Owned.Pattern();
        foreach (var entry in new DirectoryInfo(tmp).EnumerateFileSystemInfos())
        {
            if (entry.Attributes.HasFlag(FileAttributes.ReparsePoint)) continue;
            if (entry is DirectoryInfo folder && pattern.IsMatch(folder.Name) && ClaimIs(folder.FullName + ClaimSuffix, owner, mine))
            {
                found.Add(folder.FullName);
            }
            else if (entry is FileInfo claim && claim.Name.EndsWith(ClaimSuffix, StringComparison.Ordinal)
                && pattern.IsMatch(claim.Name) && !Directory.Exists(claim.FullName[..^ClaimSuffix.Length]) && ClaimIs(claim.FullName, owner, mine))
            {
                found.Add(claim.FullName);
            }
        }

        return found;
    }

    private static bool ClaimIs(string claim, Owner owner, Func<string, bool>? mine) =>
        Scope.ReadClaimText(claim, mine) is ClaimRead.Text text && Owner.Parse(text.Value) == owner;

    /// <summary>The message of a run that left something, or null when it left nothing. An empty group is omitted; a path both
    /// registered and found in the temp root is named once, under the registry's words.</summary>
    public static string? Verdict(IReadOnlyList<(string Path, bool DisposeAttempted)> unreclaimed, IReadOnlyList<string> claimed)
    {
        var registered = unreclaimed.Select(u => u.Path).ToHashSet(StringComparer.Ordinal);
        var onlyClaimed = claimed.Where(path => !registered.Contains(path)).ToList();
        var never = unreclaimed.Where(u => !u.DisposeAttempted).Select(u => u.Path).ToList();
        var failed = unreclaimed.Where(u => u.DisposeAttempted).Select(u => u.Path).ToList();
        var count = never.Count + failed.Count + onlyClaimed.Count;
        if (count == 0) return null;
        var groups = new List<string>();
        if (never.Count > 0) groups.Add("never disposed: " + string.Join(", ", never));
        if (failed.Count > 0) groups.Add("disposed but not removed: " + string.Join(", ", failed));
        if (onlyClaimed.Count > 0) groups.Add("claimed in the temp root: " + string.Join(", ", onlyClaimed));
        return $"{OwnedToken.RepoToken} tests: this run left {count} folder(s) it claimed — " + string.Join("; ", groups);
    }

    /// <summary>The audit: take the snapshot of <paramref name="registry"/>, dispose each of them once more (a handle released since
    /// lets this one succeed), then remove what <see cref="ClaimedBy"/> finds, and throw ONE exception naming everything that was
    /// left — the folders are gone by then, so the next run starts clean and the red is this run's.</summary>
    public static void Run(string tmp, Owner owner, IReadOnlyList<OwnedDir> registry, Func<string, bool>? mine = null)
    {
        var left = registry.Select(d => (d.Path, d.DisposeAttempted)).ToList();
        foreach (var dir in registry) dir.Dispose();
        var claimed = ClaimedBy(tmp, owner, mine);
        foreach (var path in claimed)
        {
            if (path.EndsWith(ClaimSuffix, StringComparison.Ordinal)) Removal.Claim(path[..^ClaimSuffix.Length], Owned.DefaultWarn);
            else Removal.Quietly(path, Owned.DefaultWarn);
        }

        if (Verdict(left, claimed) is { } message) throw new InvalidOperationException(message);
    }
}
