using FluentAssertions;
using Mcp.Diagnostics;
using Serilog.Events;
using Serilog.Parsing;
using Xunit;

namespace Mcp.Tests;

/// <summary>The two rules that had never been held against each other: <b>a file per run</b>, because the
/// question asked of a log is "what did THAT run do" — and <b>this process never restarts</b>, which is
/// what the 24/7 deployment premise says. Together they produce one file growing for months.
/// <para>The reconciliation is a segment at UTC midnight: the run stays identifiable by its pid, and no
/// single file holds more than a day.</para></summary>
public sealed class DailyRunFileSinkTests
{
    private const string Template = "[{Utc:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}";

    [Fact]
    public void A_run_writes_its_first_file_named_for_the_moment_it_started()
    {
        var root = TempRoot();

        using var sink = new DailyRunFileSink(root, "mcp-test", Template, At("2026-08-16T15:00:00"));

        Relative(root, sink.CurrentPath).Should().Be($"2026-08-16/mcp-test-15-00-00-{Environment.ProcessId}.log");
    }

    [Fact]
    public void A_run_that_outlives_the_day_continues_in_a_midnight_segment_under_the_next_days_folder()
    {
        var root = TempRoot();
        using var sink = new DailyRunFileSink(root, "mcp-test", Template, At("2026-08-16T15:00:00"));
        var first = sink.CurrentPath;

        sink.Emit(Event("before midnight", "2026-08-16T23:59:59"));
        sink.Emit(Event("after midnight", "2026-08-17T00:00:01"));

        // Named 00-00-00, not 00-00-01: the segment BEGINS at the boundary. A reader comparing it with
        // yesterday's file should see the two meet, not a gap of however long the host was quiet.
        Relative(root, sink.CurrentPath).Should().Be($"2026-08-17/mcp-test-00-00-00-{Environment.ProcessId}.log");
        sink.CurrentPath.Should().NotBe(first);
    }

    [Fact]
    public void Each_segment_holds_only_its_own_days_events()
    {
        var root = TempRoot();
        var sink = new DailyRunFileSink(root, "mcp-test", Template, At("2026-08-16T15:00:00"));
        var first = sink.CurrentPath;

        sink.Emit(Event("before midnight", "2026-08-16T23:59:59"));
        sink.Emit(Event("after midnight", "2026-08-17T00:00:01"));
        sink.Dispose();

        File.ReadAllText(first).Should().Contain("before midnight").And.NotContain("after midnight");
        File.ReadAllText(sink.CurrentPath).Should().Contain("after midnight").And.NotContain("before midnight");
    }

    [Fact]
    public void Both_segments_carry_the_same_pid_so_the_run_is_still_one_thing()
    {
        var root = TempRoot();
        using var sink = new DailyRunFileSink(root, "mcp-test", Template, At("2026-08-16T15:00:00"));

        sink.Emit(Event("day two", "2026-08-17T04:00:00"));

        // This is what keeps the segmenting from becoming the rolling-by-day sink the family rule
        // forbids: rolling by day merges every run into one file, while these two files belong to one
        // run and say so.
        Directory.EnumerateFiles(root, "*.log", SearchOption.AllDirectories)
            .Select(Path.GetFileName)
            .Should().OnlyContain(name => name!.EndsWith($"-{Environment.ProcessId}.log", StringComparison.Ordinal))
            .And.HaveCount(2);
    }

    [Fact]
    public void An_event_stamped_before_the_open_segment_does_not_reopen_the_previous_day()
    {
        var root = TempRoot();
        using var sink = new DailyRunFileSink(root, "mcp-test", Template, At("2026-08-16T15:00:00"));
        sink.Emit(Event("day two", "2026-08-17T04:00:00"));
        var afterRoll = sink.CurrentPath;

        // A clock correction, or an event queued before the boundary and delivered after it. Rolling
        // BACKWARD would reopen yesterday's file and orphan today's; a late line in the open file is
        // the lesser loss.
        sink.Emit(Event("a straggler", "2026-08-16T23:00:00"));

        sink.CurrentPath.Should().Be(afterRoll);
    }

    private static string TempRoot() => Directory.CreateTempSubdirectory("mcp-daily-log").FullName;

    private static string Relative(string root, string path) =>
        Path.GetRelativePath(Path.Combine(root, McpLogging.LogFolder), path).Replace('\\', '/');

    private static DateTimeOffset At(string utc) =>
        DateTimeOffset.Parse(utc + "Z", System.Globalization.CultureInfo.InvariantCulture);

    private static LogEvent Event(string message, string utc) =>
        new(At(utc),
            LogEventLevel.Information,
            exception: null,
            new MessageTemplateParser().Parse(message),
            []);
}
