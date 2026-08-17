using System.Text.Json;
using FluentAssertions;
using Mcp.Contracts;
using Mcp.Telemetry;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Mcp.Tests;

/// <summary>
/// The canonical spool a CONSUMER receives, emitted by the real sink and left on disk so the consumer's
/// fixture can be regenerated rather than hand-written.
///
/// <para><b>Why this test exists at all.</b> `dew_flow_benchmark` reads this wire format with its own codec
/// and must never reference this assembly — that separation is the whole point of a file format standing
/// between the two repositories. Which leaves exactly one honest way to test the contract: the emitter
/// writes a file, the consumer parses that file. A fixture the consumer hand-authored would prove only that
/// its codec agrees with somebody's idea of this emitter.</para>
///
/// <para><b>Why two records rather than one.</b> `correlation` was added to `telemetry/v0` additively, so a
/// reader today must handle both a line that carries it and a line written before it existed. One file
/// cannot be both, and trying to make it be both is what broke: the consumer's own
/// `TelemetryCorrelationTests` asserts every fixture line is UNATTRIBUTED, so replacing that file with
/// today's output turns a passing suite red for the right reason and the wrong file. Hence a correlated
/// fixture here, beside the pre-correlation one the consumer keeps as its backward-compatibility evidence.
/// </para>
///
/// <para>The emitted file lands under the test binary's own directory — never in the repository — and is
/// copied across by hand when the wire shape changes. Deliberately manual: a fixture that regenerated
/// itself on every build would silently absorb the very change it exists to catch.</para>
/// </summary>
public sealed class SpoolFixtureTests
{
    /// <summary>Where a regenerated fixture is left, relative to the test binary.</summary>
    public const string FixtureName = "mcp-spool-v0-correlated.jsonl";

    /// <summary>The clock and the values are fixed, because the consumer asserts on them. A fixture whose
    /// timestamps moved every run would force its reader's tests to assert on nothing in particular.</summary>
    private static readonly DateTimeOffset SpoolOpened = new(2026, 8, 15, 9, 30, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset CallAt = new(2026, 8, 15, 9, 30, 1, TimeSpan.Zero);

    [Fact]
    public async Task The_emitter_writes_a_correlated_spool_a_consumer_can_regenerate_its_fixture_from()
    {
        var root = Directory.CreateTempSubdirectory("mcp-spool-fixture").FullName;
        var sink = new SpoolUsageSink(
            new SpoolOptions
            {
                Directory = root,
                App = "mcp-test",
                Correlation = TelemetryCorrelation.Of("cell-17/verify"),
            },
            new FakeTimeProvider(SpoolOpened),
            NullLogger<SpoolUsageSink>.Instance);

        // The same three outcomes, in the same order, as the pre-correlation fixture: answered, refused,
        // error. Holding the order fixed is what lets the consumer index into the lines by meaning.
        foreach (var outcome in new[] { ToolOutcome.Answered, ToolOutcome.Refused, ToolOutcome.Error })
        {
            await sink.RecordAsync(Usage(outcome), TestContext.Current.CancellationToken);
        }

        await sink.DisposeAsync();

        var lines = await File.ReadAllLinesAsync(sink.Path, TestContext.Current.CancellationToken);
        lines.Should().HaveCount(3);

        var first = JsonDocument.Parse(lines[0]).RootElement;
        first.GetProperty("schema").GetString().Should().Be("telemetry/v0",
            "correlation was additive — a new field does not make a new schema version");
        var correlation = first.GetProperty("correlation");
        correlation.GetProperty("leg").GetProperty("value").GetString().Should().Be("cell-17");
        correlation.GetProperty("phase").GetProperty("value").GetString().Should().Be("verify");

        // Left where a human can pick it up. Path is reported so the copy is a command, not a hunt.
        var emitted = Path.Combine(AppContext.BaseDirectory, FixtureName);
        await File.WriteAllTextAsync(
            emitted, string.Join('\n', lines) + '\n', TestContext.Current.CancellationToken);
        TestContext.Current.SendDiagnosticMessage($"fixture written: {emitted}");

        File.Exists(emitted).Should().BeTrue();
    }

    private static ToolUsage Usage(ToolOutcome outcome) =>
        new(
            "rt_read_local_file",
            CallAt,
            new CallerIdentity(
                Captured.Text("claude-code"),
                Captured.Text("2.0.0"),
                Captured.Unavailable("the MCP protocol carries no model identity for the caller"),
                "stdio"),
            "D:/work/repo",
            """{"path":"a.txt"}""",
            0,
            outcome,
            string.Empty,
            42,
            "lines 1-3 of 3",
            0,
            // Not captured, and that is the correct state rather than a gap: this tool reads a file. The
            // consumer's own CapturedCount doc draws the same line — "a tool that embeds or reranks knows
            // its tokens; a file read does not, and the difference must be visible rather than rendered as
            // zero."
            CapturedCount.Unavailable("a file read spends no model tokens; this surface counts none"),
            TimeSpan.FromMilliseconds(13.4));
}
