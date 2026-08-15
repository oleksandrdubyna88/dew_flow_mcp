using System.Threading.Channels;
using Mcp.Contracts;
using Microsoft.Extensions.Logging;

namespace Mcp.Telemetry;

/// <summary>Where the spool is written and how much of a backlog it will hold.</summary>
public sealed record SpoolOptions
{
    /// <summary>The spool root. Empty means the sink is not configured and must not be registered.</summary>
    public required string Directory { get; init; }

    /// <summary>How this host names itself in every line — one machine runs several.</summary>
    public string App { get; init; } = "mcp";

    /// <summary>Records held in memory while the writer drains. Bounded on purpose: an unbounded queue
    /// in front of a disk turns a slow disk into a memory leak, and the failure arrives much later and
    /// somewhere else.</summary>
    public int Capacity { get; init; } = 4096;
}

/// <summary>Appends one JSON line per tool call to a spool file, for whoever owns the schema to drain.
/// <para>
/// The rule this sink is built around: <b>telemetry may never fail or delay a tool call.</b> Recording
/// hands the record to a bounded channel and returns; a background writer does the IO. A full channel
/// DROPS and counts, because blocking the caller would make an observability feature into an outage,
/// and a failing disk trips a breaker with one log line instead of one per call.
/// </para>
/// <para>
/// One file per run under a day folder, UTC throughout, mirroring the logging convention — a spool
/// that appends every run into one file is a file nobody can hand over while the host is up.
/// </para></summary>
public sealed class SpoolUsageSink : IUsageSink, IAsyncDisposable
{
    private readonly Channel<TelemetryRecord> queue;
    private readonly EmitterWire emitter;
    private readonly ILogger<SpoolUsageSink> logger;
    private readonly Task writer;
    private readonly string path;

    private int dropped;
    private bool broken;

    public SpoolUsageSink(SpoolOptions options, TimeProvider clock, ILogger<SpoolUsageSink> logger)
    {
        this.logger = logger;
        emitter = new EmitterWire(options.App, System.Environment.ProcessId, System.Environment.MachineName);
        queue = Channel.CreateBounded<TelemetryRecord>(new BoundedChannelOptions(options.Capacity)
        {
            // Wait, paired with TryWrite — which does NOT wait, it refuses. The obvious-looking
            // DropWrite is the trap: it reports success while discarding the record, so the drop
            // counter below stays at zero and a full spool becomes indistinguishable from a quiet
            // server. Measured here at capacity 1: 500 records recorded, 2 written, 0 counted.
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
        });

        path = SpoolPath(options.Directory, options.App, clock.GetUtcNow());
        writer = Task.Run(DrainAsync);
    }

    /// <summary>How many records the spool refused because the writer could not keep up. Read by tests
    /// and worth logging at shutdown: a silent drop is indistinguishable from a call that never
    /// happened, which is the one reading this data must never invite.</summary>
    public int Dropped => Volatile.Read(ref dropped);

    /// <summary>The file this sink is writing. Fixed at construction — one file per run.</summary>
    public string Path => path;

    public Task RecordAsync(ToolUsage usage, CancellationToken cancellationToken)
    {
        if (!queue.Writer.TryWrite(TelemetryRecord.From(usage, emitter)))
        {
            Interlocked.Increment(ref dropped);
        }

        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        queue.Writer.TryComplete();
        await writer;

        if (Dropped > 0)
        {
            logger.LogWarning(
                "Telemetry spool dropped {DroppedCount} record(s) because the writer could not keep up",
                Dropped);
        }
    }

    private async Task DrainAsync()
    {
        try
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        }
        catch (IOException ex)
        {
            Break(ex);
            return;
        }
        catch (UnauthorizedAccessException ex)
        {
            Break(ex);
            return;
        }

        await foreach (var record in queue.Reader.ReadAllAsync())
        {
            if (broken)
            {
                Interlocked.Increment(ref dropped);
                continue;
            }

            await WriteAsync(record);
        }
    }

    private async Task WriteAsync(TelemetryRecord record)
    {
        try
        {
            await File.AppendAllTextAsync(path, TelemetryJson.Line(record) + System.Environment.NewLine);
        }
        catch (IOException ex)
        {
            Break(ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            Break(ex);
        }
    }

    /// <summary>One line, once. A disk that has stopped accepting writes will not start because we
    /// logged about it a thousand more times — and the log is what the operator is reading.</summary>
    private void Break(Exception ex)
    {
        broken = true;
        Interlocked.Increment(ref dropped);
        logger.LogError(ex, "Telemetry spool at {SpoolPath} stopped accepting writes; recording is off for this run", path);
    }

    /// <summary>A folder per day, a file per RUN — never a rolling append across runs, because the
    /// question asked of a spool is always "hand me what this host produced", and a shared file cannot
    /// be handed over while it is being written.</summary>
    private static string SpoolPath(string root, string app, DateTimeOffset now)
    {
        var utc = now.UtcDateTime;
        return System.IO.Path.Combine(
            root,
            utc.ToString("yyyy-MM-dd"),
            $"{app}-{utc:HH-mm-ss}-{System.Environment.ProcessId}.jsonl");
    }
}
