using FluentAssertions;
using Mcp.Diagnostics;
using Xunit;

namespace Mcp.Tests;

/// <summary>The other half of the never-restarting problem. Segmenting at midnight bounds any ONE file to a
/// day; nothing bounded the total until this. A process running for months on an unattended machine fills a
/// disk with text otherwise, and the failure arrives as something else entirely.
/// <para>The key, the default and the signature are the family's, shared with <c>RagLogging</c> and
/// <c>BenchLogging</c>. This repository first shipped its own answer to the same question and had to be
/// brought back — a second answer is a mirror that has already drifted.</para></summary>
public sealed class LogRetentionTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 16, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_day_folder_older_than_the_window_is_pruned_at_startup()
    {
        var root = Root(("2026-06-01", "old.log"), ("2026-08-15", "recent.log"));

        var pruned = McpLogging.PruneLogFolders(root, retentionDays: 30, Now);

        pruned.Should().Equal("2026-06-01");
        Days(root).Should().Equal("2026-08-15");
    }

    [Fact]
    public void A_folder_inside_the_window_is_kept_and_so_is_the_boundary_day()
    {
        // 30 days back from 2026-08-16 is 2026-07-17. Strictly older goes; the boundary itself stays,
        // because "keep 30 days" that quietly keeps 29 is a window nobody can reason about.
        var root = Root(("2026-07-16", "a.log"), ("2026-07-17", "b.log"), ("2026-08-16", "c.log"));

        McpLogging.PruneLogFolders(root, retentionDays: 30, Now);

        Days(root).Should().Equal("2026-07-17", "2026-08-16");
    }

    [Fact]
    public void A_retention_of_zero_keeps_everything()
    {
        var root = Root(("2020-01-01", "ancient.log"));

        var pruned = McpLogging.PruneLogFolders(root, retentionDays: 0, Now);

        // The off switch is explicit. A misread config that silently deleted a month of logs would be the
        // worst possible failure of a retention feature, so the ambiguous value does nothing.
        pruned.Should().BeEmpty();
        Days(root).Should().Equal("2020-01-01");
    }

    [Fact]
    public void A_folder_whose_name_is_not_a_date_is_never_touched()
    {
        var root = Root(("2020-01-01", "ancient.log"), ("keep-this", "notes.txt"), ("2026-13-45", "bogus.log"));

        McpLogging.PruneLogFolders(root, retentionDays: 30, Now);

        // Anything under logs/ that is not a day folder was put there by a person, and is not this
        // routine's to delete — including a name that LOOKS like a date and is not one.
        Days(root).Should().Equal("2026-13-45", "keep-this");
    }

    [Fact]
    public void A_logs_folder_that_does_not_exist_yet_is_not_an_error()
    {
        var root = Directory.CreateTempSubdirectory("mcp-retention-empty").FullName;

        var prune = () => McpLogging.PruneLogFolders(root, retentionDays: 30, Now);

        prune.Should().NotThrow("the first run of a fresh checkout has no logs to prune");
    }

    [Fact]
    public void A_folder_that_cannot_be_removed_is_skipped_rather_than_thrown()
    {
        var root = Root(("2020-01-01", "held.log"));

        // A log viewer holding a file open is the ordinary case, and it must never stop a host from
        // starting — the one thing a retention sweep must not cost.
        using var held = File.Open(
            Path.Combine(root, McpLogging.LogFolder, "2020-01-01", "held.log"),
            FileMode.Open, FileAccess.Read, FileShare.None);

        var pruned = McpLogging.PruneLogFolders(root, retentionDays: 30, Now);

        pruned.Should().BeEmpty("a folder that refused is not a folder that went");
        Days(root).Should().Equal("2020-01-01");
    }

    private static string Root(params (string Day, string File)[] folders)
    {
        var root = Directory.CreateTempSubdirectory("mcp-retention").FullName;
        foreach (var (day, file) in folders)
        {
            var folder = Path.Combine(root, McpLogging.LogFolder, day);
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, file), "a line");
        }

        return root;
    }

    private static IEnumerable<string> Days(string root) =>
        Directory.EnumerateDirectories(Path.Combine(root, McpLogging.LogFolder))
            .Select(d => new DirectoryInfo(d).Name)
            .Order(StringComparer.Ordinal);
}
