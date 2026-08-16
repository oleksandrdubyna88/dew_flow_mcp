using System.Globalization;
using Serilog;

namespace Mcp.Diagnostics;

/// <summary>
/// Deletes log day-folders older than a window, once, at startup.
///
/// <para><b>Why startup and not a timer.</b> A background sweep is a second thing that can fail silently in
/// a process nobody is watching, and the window it enforces is measured in days — a process that never
/// restarts is exactly the case <see cref="DailyRunFileSink"/> already handles by segmenting, so the folders
/// this deletes belong to runs that ended. One pass, at the one moment there is a person nearby.</para>
///
/// <para><b>Why only the log.</b> The telemetry spool looks like the same problem and is not: it is DRAINED
/// by a consumer (<c>bench telemetry ingest</c> in <c>dew_flow_benchmark</c>, which owns
/// <c>bench telemetry prune</c> too). Deleting a spool file here would destroy records nobody has ingested,
/// and the emitter cannot know which those are. Its retention owner is the ingester, named rather than
/// assumed.</para>
///
/// <para>Nothing here may stop a host. A log folder that cannot be removed — a file held open by a viewer,
/// a permission that changed — is counted and reported, never thrown.</para>
/// </summary>
public static class LogRetention
{
    /// <summary>Where the window is configured. Days. <b>Zero or negative keeps everything</b>, which is a
    /// deliberate off switch rather than an accident: a misread config that silently deleted a month of logs
    /// would be the worst possible failure of a retention feature.</summary>
    public const string WindowDaysKey = "Mcp:Logs:RetentionDays";

    /// <summary>What a host that configures nothing gets. Long enough to cover a fortnight's investigation
    /// twice over, short enough that an unattended machine does not fill up on text.</summary>
    public const int DefaultWindowDays = 30;

    /// <summary>Removes every day-folder strictly older than the window.
    /// <para>Only folders whose NAME parses as <c>yyyy-MM-dd</c> are candidates — anything else under
    /// <c>logs/</c> was put there by a person and is not this routine's to delete.</para></summary>
    /// <returns>How many folders went, and how many refused.</returns>
    public static (int Removed, int Failed) Prune(
        string contentRoot, int windowDays, DateTimeOffset now, ILogger? logger = null)
    {
        if (windowDays <= 0)
        {
            return (0, 0);
        }

        var root = Path.Combine(contentRoot, McpLogging.LogFolder);
        if (!Directory.Exists(root))
        {
            return (0, 0);
        }

        var oldest = DateOnly.FromDateTime(now.UtcDateTime).AddDays(-windowDays);
        var removed = 0;
        var failed = 0;

        foreach (var folder in Directory.EnumerateDirectories(root))
        {
            if (!Expired(folder, oldest))
            {
                continue;
            }

            if (TryDelete(folder, logger))
            {
                removed++;
            }
            else
            {
                failed++;
            }
        }

        Report(removed, failed, windowDays, logger);
        return (removed, failed);
    }

    /// <summary>Reads the window from configuration, falling back to the default.</summary>
    public static int WindowFrom(Microsoft.Extensions.Configuration.IConfiguration configuration) =>
        Microsoft.Extensions.Configuration.ConfigurationBinder.GetValue<int?>(configuration, WindowDaysKey)
        ?? DefaultWindowDays;

    private static bool Expired(string folder, DateOnly oldest) =>
        DateOnly.TryParseExact(
            new DirectoryInfo(folder).Name, "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var day)
        && day < oldest;

    private static bool TryDelete(string folder, ILogger? logger)
    {
        try
        {
            Directory.Delete(folder, recursive: true);
            return true;
        }
        catch (Exception fault) when (fault is IOException or UnauthorizedAccessException)
        {
            // A folder held open by a log viewer is the ordinary case, and it is not a reason to fail a
            // start. Named at Warning so a folder that refuses every day is visible rather than silent.
            logger?.Warning(fault, "Could not remove the log folder {LogFolder}", folder);
            return false;
        }
    }

    private static void Report(int removed, int failed, int windowDays, ILogger? logger)
    {
        if (removed == 0 && failed == 0)
        {
            return;
        }

        logger?.Information(
            "Log retention: removed {RemovedFolders} day folder(s) older than {WindowDays} day(s), {FailedFolders} refused",
            removed, windowDays, failed);
    }
}
