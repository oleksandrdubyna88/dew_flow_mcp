using System.Globalization;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace Mcp.Diagnostics;

/// <summary>
/// The run's log on disk: a folder per day, a file per run — and, for a run that outlives the day, a new
/// file at UTC midnight.
///
/// <para><b>Why the midnight segment exists.</b> "A file per run" and "this process never restarts" were
/// two rules nobody had held against each other. A file per run is right because the question asked of a
/// log is almost always "what did THAT run do"; its mitigating rotation, though, IS the restart — and
/// the 24/7 premise is that there is none. One file, growing for months, is what the two rules produced
/// together. Segmenting at the day boundary keeps the run identifiable (same pid, consecutive days) and
/// bounds any single file to one day of traffic.</para>
///
/// <para>The boundary is the CLOCK, not twenty-four hours after startup: a run that starts at 15:00
/// writes <c>…-15-00-00-1234.log</c> and its next segment is <c>…/00-00-00-1234.log</c> in tomorrow's
/// folder. Elapsed-time segments would drift a little each day until the files stopped lining up with
/// the folders they live in, and correlating two hosts would mean arithmetic.</para>
///
/// <para>UTC throughout, like every other timestamp in the family: the Rust sidecar has no timezone
/// library and names its folder from a unix timestamp, so a local-time host and a UTC sidecar put the
/// same evening in two different day folders — and the one time anyone correlates them is while chasing
/// a failure across both.</para>
///
/// <para>The writing itself is Serilog's own file sink, one per segment, rather than a second
/// implementation of encoding, buffering and flushing. What this class owns is <i>which file</i>.</para>
/// </summary>
public sealed class DailyRunFileSink : ILogEventSink, IDisposable
{
    /// <summary>What a continuation segment is named. Not the moment the first post-midnight event
    /// happened: the segment BEGINS at the boundary, and a reader comparing it against the previous
    /// day's file should see the two meet rather than a gap of however long the host was quiet.</summary>
    private const string SegmentStart = "00-00-00";

    private readonly string root;
    private readonly string appName;
    private readonly string template;
    private readonly object gate = new();

    private DateOnly day;
    private Logger current;
    private volatile string path;

    public DailyRunFileSink(string contentRoot, string appName, string outputTemplate, DateTimeOffset startedAt)
    {
        root = Path.Combine(contentRoot, McpLogging.LogFolder);
        this.appName = appName;
        template = outputTemplate;

        day = DateOnly.FromDateTime(startedAt.UtcDateTime);
        path = FilePath(root, appName, day, startedAt.UtcDateTime.ToString("HH-mm-ss", CultureInfo.InvariantCulture));
        current = Open(path, template);
    }

    /// <summary>The file being written right now. It changes at every midnight the process lives through.
    /// </summary>
    public string CurrentPath => path;

    public void Emit(LogEvent logEvent)
    {
        var utcDay = DateOnly.FromDateTime(logEvent.Timestamp.UtcDateTime);

        lock (gate)
        {
            // FORWARD only. An event stamped earlier than the current segment — a clock correction, or
            // a queued event overtaken by the boundary — lands in the open file rather than reopening
            // yesterday's and orphaning today's. A late line in the right run beats a lost file.
            if (utcDay > day)
            {
                Roll(utcDay);
            }

            current.Write(logEvent);
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            current.Dispose();
        }
    }

    /// <summary>Where a run STARTING at this instant writes its first file. The one place the family's
    /// path shape is spelled out; <see cref="McpLogging.RunFilePath(string, string)"/> delegates here.
    /// </summary>
    public static string RunFilePath(string contentRoot, string appName, DateTimeOffset at) =>
        FilePath(
            Path.Combine(contentRoot, McpLogging.LogFolder),
            appName,
            DateOnly.FromDateTime(at.UtcDateTime),
            at.UtcDateTime.ToString("HH-mm-ss", CultureInfo.InvariantCulture));

    private void Roll(DateOnly to)
    {
        current.Dispose();
        day = to;
        path = FilePath(root, appName, to, SegmentStart);
        current = Open(path, template);
    }

    private static string FilePath(string logFolder, string appName, DateOnly utcDay, string time)
    {
        var folder = Path.Combine(logFolder, utcDay.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(folder);
        return Path.Combine(folder, $"{appName}-{time}-{Environment.ProcessId}.log");
    }

    /// <summary><c>MinimumLevel.Verbose</c> deliberately: everything arriving here has already passed the
    /// outer logger's filter, and an inner default of Information would silently discard the Debug lines
    /// somebody raised the level to see — the failure being invisible in the file rather than on screen.
    /// </summary>
    private static Logger Open(string file, string template) =>
        new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.File(
                file,
                outputTemplate: template,
                shared: false,
                flushToDiskInterval: TimeSpan.FromSeconds(2))
            .CreateLogger();
}
