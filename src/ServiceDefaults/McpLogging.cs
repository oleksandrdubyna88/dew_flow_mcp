using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;

namespace Mcp.Diagnostics;

/// <summary>
/// The one place this repository configures logging: coloured to the console, and on disk with a new file per
/// run. Every host calls it; nothing else touches a sink.
/// </summary>
public static class McpLogging
{
    /// <summary>Where a run's file lands, relative to the content root.</summary>
    public const string LogFolder = "logs";

    /// <summary>How long a day-folder survives when configuration names no window.
    /// <para>
    /// This repository's named retention owner is <b>the host, at startup</b> — the first of the two the
    /// shared logging rule allows. A file per run with no reaper is a disk that eventually fills, and on a
    /// machine running 24/7 the "eventually" is a date. Override with <c>Serilog:RetentionDays</c>; set it
    /// to zero to keep everything, which is only correct when an operator job owns the folder instead.
    /// </para>
    /// <para>
    /// The same key, the same default and the same shape as <c>dew_flow_rag_qln</c>'s <c>RagLogging</c> and
    /// <c>dew_flow_benchmark</c>'s <c>BenchLogging</c> — deliberately, and the hard way: this repository
    /// first shipped its own answer (a <c>Mcp:Logs:RetentionDays</c> key, a 30-day window, a separate class)
    /// without looking at what the family had already decided. A second answer to one question is a mirror
    /// that has already drifted, which is precisely what these three files exist to prevent.
    /// </para></summary>
    public const int DefaultRetentionDays = 14;

    private const string DayFormat = "yyyy-MM-dd";

    /// <summary>
    /// The template. Short enough to read in a terminal, structured enough to grep: the level is fixed-width
    /// so columns line up, and <c>SourceContext</c> is the type that logged — without it, a dashboard showing
    /// several hosts at once is a wall of sentences with no attribution.
    /// </summary>
    private const string Template =
        "[{Utc:HH:mm:ss} {Level:u3}] {App}/{Pid} {SourceContext}: {Message:lj}{NewLine}{Exception}";

    /// <summary>
    /// Configures Serilog and installs it as the host's logging provider.
    ///
    /// <para>Called BEFORE <c>Build()</c>, deliberately: a host that fails while wiring itself up is exactly
    /// when the log matters, and a logger installed afterwards has nothing to say about it.</para>
    /// </summary>
    /// <param name="builder">The host builder, before it is built.</param>
    /// <param name="appName">What this process is called in every line and in its file name.</param>
    /// <param name="consoleToStdErr">
    /// Send console output to stderr. Required for any host whose STDOUT carries a protocol — one log line on
    /// an MCP stdio stream corrupts the JSON-RPC and looks like a protocol bug, not a logging one.
    /// </param>
    public static void AddDewFlowLogging(
        this IHostApplicationBuilder builder, string appName, bool consoleToStdErr = false)
    {
        Log.Logger = CreateLogger(
            builder.Configuration, builder.Environment.ContentRootPath, appName, consoleToStdErr);
        builder.Logging.ClearProviders();
        builder.Services.AddSerilog(Log.Logger, dispose: true);
    }

    /// <summary>
    /// The logger itself, without a host.
    ///
    /// <para>Separate from the extension above because an orchestrator's builder is not an
    /// <see cref="IHostApplicationBuilder"/> — it has its own type — and the alternative was either a second
    /// copy of every decision here or an Aspire reference inside a project that has no business carrying one.
    /// The rules live in this method; the extension is a two-line adapter over it.</para>
    /// </summary>
    public static Serilog.ILogger CreateLogger(
        IConfiguration appConfiguration, string contentRoot, string appName, bool consoleToStdErr = false)
    {
        var configuration = new LoggerConfiguration()
            .ReadFrom.Configuration(appConfiguration)
            .Enrich.FromLogContext()
            .Enrich.With(new UtcTimestampEnricher())
            .Enrich.WithProperty("App", appName)
            .Enrich.WithProperty("Pid", Environment.ProcessId);

        ApplyDefaultLevels(configuration, appConfiguration);

        // Our own sink rather than Serilog's console theme, and the reason is measured rather than stylistic —
        // see AnsiConsoleSink: on Serilog.Sinks.Console 6.1.1 the theme emits NO escapes once stdout is
        // redirected, which is exactly the case an orchestrator creates. The documented
        // applyThemeToRedirectedOutput flag does not change that.
        configuration.WriteTo.Sink(new AnsiConsoleSink(formatProvider: null, toStandardError: consoleToStdErr));

        // No theme on the file: escape codes in a file are noise to every reader, grep included.
        //
        // Through DailyRunFileSink rather than WriteTo.File directly, because a file per run and a
        // process that never restarts are one file growing for months. It segments at UTC midnight and
        // delegates the writing to Serilog's own file sink — see that class for why the boundary is the
        // clock rather than elapsed time.
        configuration.WriteTo.Sink(
            new DailyRunFileSink(contentRoot, appName, Template, DateTimeOffset.UtcNow));

        var logger = configuration.CreateLogger();
        Retire(logger, contentRoot, RetentionDays(appConfiguration));
        InstallUnobservedTaskNet(logger);
        return logger;
    }

    /// <summary>The logger the unobserved-task net writes through — the most recently created one,
    /// which in a real process is the only one.</summary>
    private static Serilog.ILogger unobservedNetLogger = Serilog.Log.Logger;

    private static int unobservedNetInstalled;

    /// <summary>The net under the net (reliability.md): a <see cref="Task"/> whose fault nobody
    /// awaits surfaces only at finalization, where modern .NET swallows it silently instead of
    /// crashing — which is worse, because nothing logs and the worker just stops existing. Every
    /// KNOWN detached execution in this repository already observes its own fault; this catches the
    /// future one that forgets.
    /// <para>Installed from <see cref="CreateLogger"/> so every host — the extension's and the
    /// orchestrator's alike — gets it with the logger it was creating anyway. Subscribed once per
    /// process; a later logger takes over the writing, which is what a reconfigured test expects.</para></summary>
    public static void InstallUnobservedTaskNet(Serilog.ILogger logger)
    {
        unobservedNetLogger = logger;
        if (Interlocked.Exchange(ref unobservedNetInstalled, 1) == 0)
        {
            TaskScheduler.UnobservedTaskException += ObserveUnobservedTask;
        }
    }

    /// <summary>Public so a test can hand it a constructed event and watch it observe — the
    /// finalizer-driven path this rides on is not something a suite can await deterministically.</summary>
    public static void ObserveUnobservedTask(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        unobservedNetLogger.Error(
            e.Exception,
            "An unobserved task exception reached finalization — some detached task died with nobody awaiting it");
        e.SetObserved();
    }

    /// <summary>
    /// Retires day-folders past the window, at startup, best effort.
    ///
    /// <para>Startup is the right moment and not merely a convenient one: it is cheap, it is idempotent, and a
    /// host that never restarts is not producing new files either. Best effort because a folder another host
    /// still holds open is a reason to skip it, never a reason to fail the run that was about to start.</para>
    ///
    /// <para>It is the OTHER half of the never-restarting problem. <see cref="DailyRunFileSink"/> bounds any
    /// one file to a day; without this, nothing bounds the total, and a disk fills with text while the
    /// failure arrives disguised as something else.</para>
    ///
    /// <para><b>The telemetry spool is deliberately not covered here.</b> It looks like the same problem and
    /// is not: a spool file is DRAINED by a consumer, and this process cannot know which records that
    /// consumer has taken. Its retention owner is the ingester, named rather than assumed.</para>
    /// </summary>
    public static IReadOnlyList<string> PruneLogFolders(string contentRoot, int retentionDays, DateTimeOffset now)
    {
        var root = Path.Combine(contentRoot, LogFolder);

        if (retentionDays <= 0 || !Directory.Exists(root))
        {
            return [];
        }

        var cutoff = now.UtcDateTime.Date.AddDays(-retentionDays);

        return [.. Directory.EnumerateDirectories(root).Where(folder => Expired(folder, cutoff) && Delete(folder))
            .Select(Path.GetFileName)!];
    }

    /// <summary>Whether this folder is a day-folder old enough to retire.
    /// <para>
    /// A name that does not parse as a day is never expired, and therefore never deleted. That is the whole
    /// safety property: this method decides to remove a directory tree, so anything it does not positively
    /// recognise stays where it is.
    /// </para></summary>
    private static bool Expired(string folder, DateTime cutoff) =>
        DateTime.TryParseExact(
            Path.GetFileName(folder),
            DayFormat,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var day)
        && day < cutoff;

    private static bool Delete(string folder)
    {
        try
        {
            Directory.Delete(folder, recursive: true);
            return true;
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            // Another host is writing in there, or the file system said no. Retention is not worth failing
            // a startup over — the next run tries again.
            return false;
        }
    }

    private static int RetentionDays(IConfiguration configuration) =>
        int.TryParse(configuration["Serilog:RetentionDays"], out var days) ? days : DefaultRetentionDays;

    private static void Retire(Serilog.ILogger logger, string contentRoot, int retentionDays)
    {
        var pruned = PruneLogFolders(contentRoot, retentionDays, DateTimeOffset.UtcNow);

        if (pruned.Count > 0)
        {
            logger.Information(
                "Retired {Count} log day-folder(s) older than {RetentionDays} day(s): {Folders}",
                pruned.Count, retentionDays, string.Join(", ", pruned));
        }
    }

    /// <summary>
    /// Where a run starting NOW writes its first file: a folder per day, a file per run.
    ///
    /// <para>Not a rolling-by-day sink, and that is the requirement rather than a preference: rolling by
    /// day appends every run into one file, while the question actually asked is almost always "what did
    /// THAT run do". The timestamp is taken once, here; the pid disambiguates two hosts started in the
    /// same second, which an orchestrator launching several children does on every start.</para>
    ///
    /// <para>A run that outlives the day continues in a <c>00-00-00</c> segment under the next day's
    /// folder — same pid, so the run is still one thing. <see cref="DailyRunFileSink"/> owns that, and
    /// owns the path shape this method delegates to.</para>
    /// </summary>
    public static string RunFilePath(string contentRoot, string appName) =>
        DailyRunFileSink.RunFilePath(contentRoot, appName, DateTimeOffset.UtcNow);

    /// <summary>
    /// The floor, and the two sources that drown everything else at Information.
    ///
    /// <para>Applied only when the configuration file says nothing, so an operator's <c>Serilog:</c> section
    /// always wins — verbosity is a config edit, never an edited call site.</para>
    /// </summary>
    private static void ApplyDefaultLevels(LoggerConfiguration logger, IConfiguration configuration)
    {
        if (configuration.GetSection("Serilog:MinimumLevel").Exists())
        {
            return;
        }

        logger.MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
            // Every outbound call logs three lines through this category. At Information a single indexing
            // pass buries its own progress under a thousand of them — measured on a 463-member pass.
            .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Warning);
    }
}
