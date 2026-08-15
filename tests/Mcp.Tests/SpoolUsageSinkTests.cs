using System.Text.Json;
using FluentAssertions;
using Mcp.Contracts;
using Mcp.Telemetry;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Mcp.Tests;

/// <summary>The spool is the whole transport: what it writes here is what the benchmark ingests there,
/// so these tests are the emitter half of a cross-repository contract.</summary>
public sealed class SpoolUsageSinkTests
{
    [Fact]
    public async Task A_recorded_call_lands_as_one_json_line_carrying_the_schema_version()
    {
        var (sink, root) = Build();

        await sink.RecordAsync(Usage(), TestContext.Current.CancellationToken);
        await sink.DisposeAsync();

        var lines = await File.ReadAllLinesAsync(sink.Path, TestContext.Current.CancellationToken);
        lines.Should().ContainSingle("one call is one line, so a half-written file loses one call rather than the file");

        var record = JsonDocument.Parse(lines[0]).RootElement;
        record.GetProperty("schema").GetString().Should().Be(TelemetryRecord.V0);
        record.GetProperty("tool").GetString().Should().Be("rt_read_local_file");
        record.GetProperty("scope").GetString().Should().Be("D:/work/repo");
        Directory.GetFiles(root, "*.jsonl", SearchOption.AllDirectories).Should().ContainSingle();
    }

    [Fact]
    public async Task The_outcome_reaches_the_wire_as_one_of_three_named_states()
    {
        var (sink, _) = Build();

        await sink.RecordAsync(Usage() with { Outcome = ToolOutcome.Answered }, TestContext.Current.CancellationToken);
        await sink.RecordAsync(Usage() with { Outcome = ToolOutcome.Refused }, TestContext.Current.CancellationToken);
        await sink.RecordAsync(Usage() with { Outcome = ToolOutcome.Error }, TestContext.Current.CancellationToken);
        await sink.DisposeAsync();

        var outcomes = (await File.ReadAllLinesAsync(sink.Path, TestContext.Current.CancellationToken))
            .Select(line => JsonDocument.Parse(line).RootElement.GetProperty("outcome").GetString());

        // Spelled out on the wire rather than taken from the enum's ToString: these three words are a
        // published vocabulary, and renaming the C# enum must not silently rename them.
        outcomes.Should().Equal("answered", "refused", "error");
    }

    [Fact]
    public async Task An_uncaptured_field_ships_its_reason_instead_of_a_plausible_value()
    {
        var (sink, _) = Build();

        await sink.RecordAsync(Usage(), TestContext.Current.CancellationToken);
        await sink.DisposeAsync();

        var line = JsonDocument.Parse(
            (await File.ReadAllLinesAsync(sink.Path, TestContext.Current.CancellationToken))[0]).RootElement;

        var model = line.GetProperty("caller").GetProperty("model");
        model.GetProperty("captured").GetBoolean().Should().BeFalse();
        model.GetProperty("reason").GetString().Should().NotBeEmpty();

        // The flag ships even when true, so a consumer never infers "unknown" from an absent field.
        line.GetProperty("caller").GetProperty("clientName").GetProperty("captured").GetBoolean().Should().BeTrue();
        line.GetProperty("tokens").GetProperty("captured").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task The_file_is_named_for_the_run_under_a_day_folder()
    {
        var (sink, root) = Build();
        await sink.DisposeAsync();

        // A spool that appends every run into one file is a file nobody can hand over while the host
        // is up — the same reason the logging rule refuses a rolling-by-day sink.
        var relative = System.IO.Path.GetRelativePath(root, sink.Path).Replace('\\', '/');
        relative.Should().StartWith("2026-08-15/");
        relative.Should().Contain("mcp-test-");
        relative.Should().EndWith(".jsonl");
    }

    [Fact]
    public async Task Recording_never_blocks_the_caller_and_a_full_spool_drops_rather_than_waits()
    {
        var (sink, _) = Build(capacity: 1);

        // A tool call must never wait on telemetry: an unbounded queue in front of a disk turns a slow
        // disk into a memory leak, and blocking turns an observability feature into an outage.
        for (var i = 0; i < 500; i++)
        {
            var record = sink.RecordAsync(Usage(), TestContext.Current.CancellationToken);
            record.IsCompleted.Should().BeTrue("recording hands off to a background writer and returns");
        }

        await sink.DisposeAsync();
        (sink.Dropped + await CountLines(sink.Path)).Should().Be(500, "every record is either written or counted as dropped");
    }

    [Fact]
    public async Task A_spool_directory_that_cannot_be_written_never_fails_a_call()
    {
        // A file where the day folder should go: creating the directory fails, which is the shape of a
        // read-only mount or a revoked permission.
        var root = Directory.CreateTempSubdirectory("mcp-spool-broken").FullName;
        await File.WriteAllTextAsync(System.IO.Path.Combine(root, "2026-08-15"), "not a directory", TestContext.Current.CancellationToken);
        var sink = Sink(root, capacity: 64);

        var record = async () =>
        {
            await sink.RecordAsync(Usage(), TestContext.Current.CancellationToken);
            await sink.DisposeAsync();
        };

        await record.Should().NotThrowAsync("a broken spool is a lost record, never a failed tool call");
        sink.Dropped.Should().BeGreaterThan(0, "and the loss is counted rather than silent");
    }

    private static async Task<int> CountLines(string path) =>
        File.Exists(path) ? (await File.ReadAllLinesAsync(path)).Length : 0;

    private static (SpoolUsageSink Sink, string Root) Build(int capacity = 64)
    {
        var root = Directory.CreateTempSubdirectory("mcp-spool").FullName;
        return (Sink(root, capacity), root);
    }

    private static SpoolUsageSink Sink(string root, int capacity) =>
        new(new SpoolOptions { Directory = root, App = "mcp-test", Capacity = capacity },
            new FakeTimeProvider(new DateTimeOffset(2026, 8, 15, 9, 30, 0, TimeSpan.Zero)),
            NullLogger<SpoolUsageSink>.Instance);

    private static ToolUsage Usage() =>
        new(
            "rt_read_local_file",
            new DateTimeOffset(2026, 8, 15, 9, 30, 1, TimeSpan.Zero),
            new CallerIdentity(
                Captured.Text("claude-code"),
                Captured.Text("2.0.0"),
                Captured.Unavailable("the MCP protocol carries no model identity for the caller"),
                "stdio"),
            "D:/work/repo",
            """{"path":"a.txt"}""",
            0,
            ToolOutcome.Answered,
            string.Empty,
            42,
            "lines 1-3 of 3",
            0,
            CapturedCount.Unavailable("this surface does not count tokens"),
            TimeSpan.FromMilliseconds(13.4));
}
