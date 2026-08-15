using Serilog.Core;
using Serilog.Events;

namespace Mcp.Diagnostics;

/// <summary>
/// Adds the event's timestamp a second time, in UTC, as <c>Utc</c>.
///
/// <para>Serilog's own <c>{Timestamp}</c> is a <see cref="DateTimeOffset"/> carrying the machine's local
/// offset, and an output template renders it in local time with no way to ask for anything else. That put a
/// file named <c>daemon-10-39-32.log</c> — UTC, as the rule requires — full of lines stamped <c>12:39:32</c>,
/// which is the one thing worse than either clock: two clocks in one artefact, with nothing saying so.</para>
///
/// <para>A property rather than a formatter so both sinks can use it, and so the console sink's own UTC
/// rendering and the file's come from the same value.</para>
/// </summary>
public sealed class UtcTimestampEnricher : ILogEventEnricher
{
    public const string PropertyName = "Utc";

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory) =>
        logEvent.AddPropertyIfAbsent(
            new LogEventProperty(PropertyName, new ScalarValue(logEvent.Timestamp.UtcDateTime)));
}
