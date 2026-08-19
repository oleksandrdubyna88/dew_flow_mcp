using FluentAssertions;
using Mcp.Diagnostics;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Xunit;

namespace Mcp.Tests;

/// <summary>The net under the net (reliability.md): a task whose fault nobody awaits dies at
/// finalization, where modern .NET swallows it silently — no crash, no log, a worker that just stops
/// existing. The handler is tested directly with a constructed event, because the real path runs on
/// the finalizer thread and a suite cannot await a GC deterministically without flaking.</summary>
public sealed class UnobservedTaskNetTests
{
    [Fact]
    public void An_unobserved_task_fault_is_logged_and_marked_observed()
    {
        var events = new List<LogEvent>();
        McpLogging.InstallUnobservedTaskNet(
            new LoggerConfiguration().WriteTo.Sink(new ListSink(events)).CreateLogger());
        var args = new UnobservedTaskExceptionEventArgs(
            new AggregateException(new InvalidOperationException("a detached task died")));

        McpLogging.ObserveUnobservedTask(null, args);

        args.Observed.Should().BeTrue("a fault left unobserved keeps dying silently");
        var line = events.Should().ContainSingle().Subject;
        line.Level.Should().Be(LogEventLevel.Error);
        line.Exception.Should().BeOfType<AggregateException>()
            .Which.InnerExceptions.Should().ContainSingle()
            .Which.Message.Should().Be("a detached task died");
    }

    /// <summary>Keeps the captured events; six lines beat a package for one assertion.</summary>
    private sealed class ListSink(List<LogEvent> events) : ILogEventSink
    {
        public void Emit(LogEvent logEvent) => events.Add(logEvent);
    }
}
