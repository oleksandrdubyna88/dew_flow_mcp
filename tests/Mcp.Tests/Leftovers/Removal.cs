namespace Mcp.Tests.Leftovers;

/// <summary>Teardown by the run's own EXACT path (never by the prefix), five tries 100 ms apart, never throwing: it runs in
/// <c>Dispose</c>, where a throw would hide the test's own failure. The folder goes first, its claim last, so what cannot
/// be removed stays prefixed AND claimed and ONE warning names it — the start-of-run sweep and the janitor can still judge
/// it by its owner. It never asks who owns what: the run removes its own exact path, whoever the kernel says owns it.</summary>
internal static class Removal
{
    private const int Tries = 5;
    private static readonly TimeSpan Backoff = TimeSpan.FromMilliseconds(100);

    /// <summary>Top-level links first (removed as links, never what they point at), then the folder, then its claim. True
    /// only when BOTH are gone; a path already gone is success.</summary>
    internal static bool Quietly(string dir, Action<string> warn)
    {
        var failure = Retry(() =>
        {
            if (!Directory.Exists(dir)) return;
            UnlinkTopLevelLinks(dir);
            Directory.Delete(dir, recursive: true);
        });
        if (failure is not null)
        {
            warn($"owned: could not remove {dir} ({failure.GetType().Name}: {failure.Message}) — left for the start-of-run sweep");
            return false; // the claim stays with it
        }

        return Claim(dir, warn);
    }

    /// <summary>Only the claim beside <paramref name="dir"/> (the helper's own, or one whose folder was never made).</summary>
    internal static bool Claim(string dir, Action<string> warn)
    {
        var claim = dir + ".owner";
        var failure = Retry(() => File.Delete(claim)); // File.Delete of a missing file is silence
        if (failure is null) return true;
        warn($"owned: could not remove {claim} ({failure.GetType().Name}: {failure.Message}) — left for the start-of-run sweep");
        return false;
    }

    /// <summary>Remove the claim beside <paramref name="dir"/> only while it still holds exactly <paramref name="written"/>:
    /// a claim another run put there since is not ours. A claim that is not there is silence.</summary>
    internal static void ClaimIfStill(string dir, string written, Action<string> warn)
    {
        var claim = dir + ".owner";
        try
        {
            if (File.ReadAllText(claim) == written) File.Delete(claim);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            // Already gone: nothing to remove, nothing to say.
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            warn($"owned: could not remove the claim {claim} ({ex.GetType().Name}: {ex.Message}) — left for the start-of-run sweep");
        }
    }

    /// <summary>Every top-level entry that is a link (a symlink or a Windows junction) is removed AS a link — a directory
    /// link as a directory entry, a file link as a file — and never what it points at. Deeper links are left to
    /// <c>Directory.Delete(recursive)</c>, which does not follow them either.</summary>
    internal static void UnlinkTopLevelLinks(string dir)
    {
        foreach (var entry in new DirectoryInfo(dir).EnumerateFileSystemInfos())
        {
            if (!entry.Attributes.HasFlag(FileAttributes.ReparsePoint)) continue;
            if (entry is DirectoryInfo && OperatingSystem.IsWindows()) Directory.Delete(entry.FullName, recursive: false);
            else File.Delete(entry.FullName); // unlink(2): a directory symlink too, never its target
        }
    }

    /// <summary>Five tries 100 ms apart; the last exception, or null once a try succeeds. Every exception is caught.</summary>
    private static Exception? Retry(Action step)
    {
        Exception? last = null;
        for (var attempt = 0; attempt < Tries; attempt++)
        {
            if (attempt > 0) Thread.Sleep(Backoff);
            try
            {
                step();
                return null;
            }
            catch (Exception ex)
            {
                last = ex; // teardown never throws: the next try, or the one warning
            }
        }

        return last;
    }
}
