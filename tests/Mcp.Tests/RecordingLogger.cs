using Microsoft.Extensions.Logging;

namespace Mcp.Tests;

/// <summary>An <see cref="ILogger{T}"/> that keeps what it was told, so a test can assert that a
/// failure was REPORTED and not merely survived.
/// <para>The defect this exists for: a background writer that dies with no line in the log while the
/// process looks healthy. "It did not throw" is not the guarantee — "somebody can read what
/// happened" is, and only a logger that remembers can prove it.</para></summary>
public sealed class RecordingLogger<T> : ILogger<T>
{
    private readonly List<(LogLevel Level, string Message, Exception? Error)> entries = [];

    public IReadOnlyList<string> Errors => Messages(LogLevel.Error);

    public IReadOnlyList<string> Warnings => Messages(LogLevel.Warning);

    /// <summary>The exception types that reached the log — the half of a report a message string does
    /// not carry.</summary>
    public IReadOnlyList<string> ErrorTypes
    {
        get
        {
            lock (entries)
            {
                return [.. entries.Where(e => e.Level == LogLevel.Error && e.Error is not null)
                    .Select(e => e.Error!.GetType().Name)];
            }
        }
    }

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        lock (entries)
        {
            entries.Add((logLevel, formatter(state, exception), exception));
        }
    }

    private IReadOnlyList<string> Messages(LogLevel level)
    {
        lock (entries)
        {
            return [.. entries.Where(e => e.Level == level).Select(e => e.Message)];
        }
    }
}
