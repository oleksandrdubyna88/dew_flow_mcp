using System.Text.Json;
using FluentAssertions;
using Mcp.Contracts;
using Mcp.Telemetry;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Mcp.Tests;

/// <summary>The correlation half of the `telemetry/v0` contract: what unit of work the CALLER declared
/// this process to be serving, never something this server inferred.
/// <para>This is the emitter side of a cross-repository contract, so these tests assert the exact
/// property names and the exact unattributed reasons the consumer reads — two suites can both be green
/// while a wire disagrees, because each compares its own list against itself.</para></summary>
public sealed class TelemetryCorrelationTests
{
    [Fact]
    public void A_process_that_declares_nothing_is_unattributed_with_a_reason()
    {
        var correlation = TelemetryCorrelation.Of(string.Empty);

        // Absent is the honest reading for every real session; it is not a gap to be filled.
        correlation.Should().BeSameAs(TelemetryCorrelation.None);
        correlation.Leg.WasCaptured.Should().BeFalse();
        correlation.Leg.Value.Should().BeEmpty();
        correlation.Leg.Reason.Should().Be("the caller declared no leg");
        correlation.Phase.Reason.Should().Be("the caller declared no phase");
    }

    [Fact]
    public void A_leg_with_a_phase_is_read_as_both()
    {
        var correlation = TelemetryCorrelation.Of("cell-17/verify");

        correlation.Leg.WasCaptured.Should().BeTrue();
        correlation.Leg.Value.Should().Be("cell-17");
        correlation.Phase.WasCaptured.Should().BeTrue();
        correlation.Phase.Value.Should().Be("verify");
    }

    [Fact]
    public void A_leg_with_no_phase_leaves_the_phase_not_captured_rather_than_blank()
    {
        var correlation = TelemetryCorrelation.Of("cell-17");

        // A leg without a phase is a legitimate declaration, and a blank phase that reads as captured
        // would be an empty string somebody could group by.
        correlation.Leg.Value.Should().Be("cell-17");
        correlation.Phase.WasCaptured.Should().BeFalse();
        correlation.Phase.Reason.Should().NotBeEmpty();
    }

    [Fact]
    public void A_trailing_separator_declares_a_leg_and_no_phase()
    {
        var correlation = TelemetryCorrelation.Of(" cell-17/ ");

        correlation.Leg.Value.Should().Be("cell-17");
        correlation.Phase.WasCaptured.Should().BeFalse();
    }

    [Fact]
    public void A_correlation_on_the_shared_transport_is_refused_and_the_message_names_the_alternative()
    {
        var declare = () => TelemetryCorrelation.Declared("cell-17/fix", sharedTransport: true);

        // One value stamped across concurrent callers would INVENT an attribution, and an invented one
        // is worse than none: a report averaging over rows it wrongly believes belong to a leg cannot
        // be told from a correct one.
        declare.Should().Throw<InvalidOperationException>()
            .WithMessage("*cell-17/fix*--stdio*");
    }

    [Fact]
    public void The_shared_transport_is_only_refused_when_something_was_actually_declared()
    {
        var declare = () => TelemetryCorrelation.Declared(string.Empty, sharedTransport: true);

        declare.Should().NotThrow("the HTTP transport is the normal case and declares nothing");
    }

    [Fact]
    public async Task A_declared_correlation_reaches_the_wire_under_the_names_the_consumer_reads()
    {
        var line = await OneLine(TelemetryCorrelation.Of("cell-17/verify"));

        var correlation = line.GetProperty("correlation");
        correlation.GetProperty("leg").GetProperty("captured").GetBoolean().Should().BeTrue();
        correlation.GetProperty("leg").GetProperty("value").GetString().Should().Be("cell-17");
        correlation.GetProperty("leg").GetProperty("reason").GetString().Should().BeEmpty();
        correlation.GetProperty("phase").GetProperty("value").GetString().Should().Be("verify");

        // Additive within v0: the field arrives without a version bump, because the consumer was built
        // defaulting an absent correlation object to unattributed and documents that it must.
        line.GetProperty("schema").GetString().Should().Be("telemetry/v0");
    }

    [Fact]
    public async Task An_undeclared_correlation_still_ships_with_the_reason_the_consumer_uses_for_an_absent_one()
    {
        var line = await OneLine(TelemetryCorrelation.None);

        var correlation = line.GetProperty("correlation");
        correlation.GetProperty("leg").GetProperty("captured").GetBoolean().Should().BeFalse();
        correlation.GetProperty("leg").GetProperty("value").GetString().Should().BeEmpty();

        // The SAME strings the consumer substitutes when the object is missing entirely. Otherwise
        // "this line predates the field" and "this caller declared nothing" read as two different facts
        // downstream when they are one, and every aggregate over them has to know the difference.
        correlation.GetProperty("leg").GetProperty("reason").GetString()
            .Should().Be("the caller declared no leg");
        correlation.GetProperty("phase").GetProperty("reason").GetString()
            .Should().Be("the caller declared no phase");
    }

    private static async Task<JsonElement> OneLine(TelemetryCorrelation correlation)
    {
        var root = Directory.CreateTempSubdirectory("mcp-correlation").FullName;
        var sink = new SpoolUsageSink(
            new SpoolOptions { Directory = root, App = "mcp-test", Correlation = correlation },
            new FakeTimeProvider(new DateTimeOffset(2026, 8, 15, 9, 30, 0, TimeSpan.Zero)),
            NullLogger<SpoolUsageSink>.Instance);

        await sink.RecordAsync(Usage(), TestContext.Current.CancellationToken);
        await sink.DisposeAsync();

        var lines = await File.ReadAllLinesAsync(sink.Path, TestContext.Current.CancellationToken);
        return JsonDocument.Parse(lines.Single()).RootElement.Clone();
    }

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
