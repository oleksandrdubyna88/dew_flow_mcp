using FluentAssertions;
using Mcp.Api;
using Mcp.Contracts;
using Mcp.Telemetry;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Mcp.Tests;

/// <summary>What `/health` is for: an orchestrator polling a process that runs for weeks must be able
/// to see a component that has stopped working. The endpoint used to answer the constant `"ok"`, which
/// answers for the ROUTE and says nothing about the server behind it.</summary>
public sealed class HealthTests
{
    [Fact]
    public async Task A_dead_telemetry_writer_shows_up_as_degraded_with_its_drop_count()
    {
        var sink = BrokenSpool();
        await sink.RecordAsync(Usage(), TestContext.Current.CancellationToken);
        await sink.DisposeAsync();

        var health = McpApiHealth.From([sink]);

        health.Status.Should().Be(McpApiHealth.Degraded);
        var component = health.Components.Should().ContainSingle().Subject;
        component.Component.Should().Be(SpoolUsageSink.Component);
        component.Healthy.Should().BeFalse();
        component.Detail.Should().Contain("dropped", "a verdict without its numbers moves the diagnosis somewhere else");
    }

    [Fact]
    public async Task A_working_telemetry_writer_reports_ok_so_degraded_still_means_something()
    {
        var sink = HealthySpool();
        await sink.RecordAsync(Usage(), TestContext.Current.CancellationToken);

        var health = McpApiHealth.From([sink]);

        // The other half of the guarantee: a probe stuck on "degraded" is as useless as one stuck on
        // "ok", and only asserting both proves it is computed at all.
        health.Status.Should().Be(McpApiHealth.Ok);
        health.Components.Should().ContainSingle().Which.Healthy.Should().BeTrue();

        await sink.DisposeAsync();
    }

    [Fact]
    public void With_nothing_registered_health_reports_no_components_rather_than_claiming_a_check()
    {
        var health = McpApiHealth.From([]);

        health.Status.Should().Be(McpApiHealth.Ok);
        health.Components.Should().BeEmpty("an empty list says nothing was checked; a bare 'ok' would claim something was");
    }

    /// <summary>A file where the day folder should go — a read-only mount or a revoked permission in
    /// the shape a test can build.</summary>
    private static SpoolUsageSink BrokenSpool()
    {
        var root = Directory.CreateTempSubdirectory("mcp-health-broken").FullName;
        File.WriteAllText(System.IO.Path.Combine(root, "2026-08-15"), "not a directory");
        return Sink(root);
    }

    private static SpoolUsageSink HealthySpool() =>
        Sink(Directory.CreateTempSubdirectory("mcp-health").FullName);

    private static SpoolUsageSink Sink(string root) =>
        new(new SpoolOptions { Directory = root, App = "mcp-test", Capacity = 64 },
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
