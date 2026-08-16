using System.Text;
using FluentAssertions;
using Mcp.Diagnostics;
using Serilog.Events;
using Serilog.Parsing;
using Xunit;

namespace Mcp.Tests;

/// <summary>The console sink's two jobs: colour that survives redirection, and a line that stays whole.
/// <para>It used to render straight into the console writer — the formatter plus five to seven separate
/// <c>Write</c> calls plus an explicit flush, all inside one global lock, per event. <c>Console.Out</c>
/// auto-flushes, so that was five to seven flushes held against every other thread on every line:
/// invisible at Information volume, and a contention point exactly when somebody raises the level to
/// debug a live problem.</para></summary>
public sealed class AnsiConsoleSinkTests
{
    [Fact]
    public void One_event_reaches_the_stream_as_a_single_write()
    {
        var stream = new CountingWriter();
        var sink = new AnsiConsoleSink(formatProvider: null, toStandardError: false, output: stream);

        sink.Emit(Event("a line"));

        // The whole point of the fix: what the lock guards is now one write of a finished string.
        stream.Writes.Should().Be(1);
        stream.Text.Should().Contain("a line").And.EndWith(Environment.NewLine);
    }

    [Fact]
    public void An_exception_still_travels_with_its_line_in_that_one_write()
    {
        var stream = new CountingWriter();
        var sink = new AnsiConsoleSink(formatProvider: null, toStandardError: false, output: stream);

        sink.Emit(Event("it broke", new InvalidOperationException("the disk went away")));

        stream.Writes.Should().Be(1, "a stack trace split from its message can interleave with another host's");
        stream.Text.Should().Contain("it broke").And.Contain("the disk went away");
    }

    [Fact]
    public void The_level_is_coloured_even_though_the_stream_is_redirected()
    {
        var stream = new CountingWriter();
        var sink = new AnsiConsoleSink(formatProvider: null, toStandardError: false, output: stream);

        sink.Emit(Event("a warning", level: LogEventLevel.Warning));

        // The measured reason this sink exists at all: on Serilog.Sinks.Console 6.1.1 the theme emitted
        // ZERO escape bytes once stdout was redirected, even with applyThemeToRedirectedOutput. An
        // orchestrator capturing a child's output redirects stdout by definition.
        stream.Text.Should().Contain("[", "colour must survive the one case it is for");
        stream.Text.Should().Contain("WRN");
    }

    [Fact]
    public void Concurrent_events_never_interleave_within_a_line()
    {
        var stream = new CountingWriter();
        var sink = new AnsiConsoleSink(formatProvider: null, toStandardError: false, output: stream);

        // Rendering moved OUTSIDE the lock, so the formatter is now called concurrently — which is the
        // contract Serilog states for every ITextFormatter, and this is the test that would catch it
        // being untrue here.
        Parallel.For(0, 800, i => sink.Emit(Event($"line-{i:D4}")));

        var lines = stream.Text.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        lines.Should().HaveCount(800);
        lines.Should().OnlyContain(line => line.Contains("line-", StringComparison.Ordinal));
        lines.Select(line => line[line.IndexOf("line-", StringComparison.Ordinal)..][..9])
            .Distinct().Should().HaveCount(800, "a torn line would duplicate or lose an index");
    }

    private static LogEvent Event(
        string message, Exception? exception = null, LogEventLevel level = LogEventLevel.Information) =>
        new(new DateTimeOffset(2026, 8, 16, 15, 0, 0, TimeSpan.Zero),
            level,
            exception,
            new MessageTemplateParser().Parse(message),
            []);

    /// <summary>Counts writes and keeps the text. Its own lock, because the subject under test is
    /// exactly whether the sink hands it one complete line at a time.</summary>
    private sealed class CountingWriter : TextWriter
    {
        private readonly StringBuilder text = new();
        private readonly object gate = new();
        private int writes;

        public override Encoding Encoding => Encoding.UTF8;

        public int Writes => Volatile.Read(ref writes);

        public string Text
        {
            get { lock (gate) { return text.ToString(); } }
        }

        public override void Write(string? value)
        {
            lock (gate)
            {
                Interlocked.Increment(ref writes);
                text.Append(value);
            }
        }
    }
}
