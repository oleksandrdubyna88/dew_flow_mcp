using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Display;

namespace Mcp.Diagnostics;

/// <summary>
/// Writes coloured log lines to stdout (or stderr) with ANSI escapes, unconditionally.
///
/// <para><b>Why this exists instead of Serilog's own console theme.</b> The documented knob does not work.
/// Measured on Serilog.Sinks.Console 6.1.1: with <c>theme: AnsiConsoleTheme.Code</c> AND
/// <c>applyThemeToRedirectedOutput: true</c>, redirected stdout received <b>zero</b> escape bytes — as did
/// <c>AnsiConsoleTheme.Sixteen</c>, and as did a <c>Serilog.Expressions</c> <c>ExpressionTemplate</c> carrying
/// a <c>TemplateTheme</c>. A control that wrote an escape by hand in the same process produced four, so the
/// measurement was sound and the sink simply never emits colour once stdout is redirected.</para>
///
/// <para>An orchestrator capturing a child's output redirects stdout by definition, so "colour in the
/// dashboard" and "Serilog's console theme" are mutually exclusive on this version. Forty lines we own beat
/// a knob that silently does nothing.</para>
///
/// <para>The colours are deliberately few: the level is the only thing coloured strongly, because a line
/// where everything is coloured is a line where nothing stands out.</para>
///
/// <para><b>One write per event, and the line is built before the lock is taken.</b> It used to render
/// directly into the console writer — five to seven separate <c>Write</c> calls plus the formatter plus
/// an explicit flush, all inside a global lock, on every event. <c>Console.Out</c> auto-flushes, so that
/// was five to seven flushes per line held against every other thread. Invisible at Information volume
/// and a contention point exactly when somebody raises the level to debug a live problem, which is the
/// moment it must not be. The lock stays, because a line is only atomic if something makes it so; what
/// it now guards is a single <c>Write</c> of an already-finished string.</para>
/// </summary>
/// <param name="output">Where lines go. Null — the default — resolves <see cref="Console"/> at each
/// event, so a host that redirects its streams after startup is still honoured. A writer supplied here
/// is used as given, which is what makes the sink testable without touching process-global state.</param>
public sealed class AnsiConsoleSink(
    IFormatProvider? formatProvider, bool toStandardError, TextWriter? output = null) : ILogEventSink
{
    private const string Reset = "[0m";
    private const string Dim = "[38;5;245m";
    private const string Context = "[38;5;110m";

    private readonly object _gate = new();

    /// <summary>
    /// Renders the message through Serilog's own formatter with <c>:lj</c>.
    ///
    /// <para><c>RenderMessage()</c> would be the obvious call and it quotes every string property, so a
    /// connection error reads <c>database '"qln"'</c> — the quotes belong to a JSON-ish rendering nobody
    /// asked for here. <c>l</c> is literal, <c>j</c> keeps structured values readable as JSON. This is what
    /// Serilog's own console output template uses, and reusing its renderer beats re-deriving the rules.</para>
    /// </summary>
    private readonly MessageTemplateTextFormatter _message = new("{Message:lj}", formatProvider);

    public void Emit(LogEvent logEvent)
    {
        // Rendered first, outside the lock. MessageTemplateTextFormatter holds only its parsed template
        // and is safe to call concurrently — which is the contract Serilog states for every
        // ITextFormatter, because sinks call them from whichever thread logged.
        var line = Render(logEvent);
        var writer = output ?? (toStandardError ? Console.Error : Console.Out);

        // One lock, one write: two hosts logging into one captured stream interleave mid-line otherwise, and
        // a half-line with an escape sequence in it colours everything after it.
        lock (_gate)
        {
            writer.Write(line);
            writer.Flush();
        }
    }

    /// <summary>The finished line, escapes and newline included. Pure apart from reading the event.
    /// </summary>
    private string Render(LogEvent logEvent)
    {
        var source = logEvent.Properties.TryGetValue("SourceContext", out var context)
            ? Shorten(context.ToString().Trim('"'))
            : "";

        var line = new StringWriter();
        line.Write($"{Dim}[{logEvent.Timestamp.UtcDateTime:HH:mm:ss}{Reset} {LevelToken(logEvent.Level)}{Dim}]{Reset} ");
        line.Write($"{Context}{Property(logEvent, "App")}/{Property(logEvent, "Pid")}{Reset} ");
        if (source.Length > 0)
        {
            line.Write($"{Dim}{source}:{Reset} ");
        }

        _message.Format(logEvent, line);
        line.Write(Environment.NewLine);
        if (logEvent.Exception is not null)
        {
            line.Write($"{LevelColour(LogEventLevel.Error)}{logEvent.Exception}{Reset}{Environment.NewLine}");
        }

        return line.ToString();
    }

    private static string LevelToken(LogEventLevel level) =>
        $"{LevelColour(level)}{Abbreviate(level)}{Reset}";

    private static string LevelColour(LogEventLevel level) => level switch
    {
        LogEventLevel.Verbose => "[38;5;240m",
        LogEventLevel.Debug => "[38;5;244m",
        LogEventLevel.Information => "[38;5;42m",
        LogEventLevel.Warning => "[38;5;214m",
        LogEventLevel.Error => "[38;5;203m",
        _ => "[1;38;5;199m",
    };

    private static string Abbreviate(LogEventLevel level) => level switch
    {
        LogEventLevel.Verbose => "VRB",
        LogEventLevel.Debug => "DBG",
        LogEventLevel.Information => "INF",
        LogEventLevel.Warning => "WRN",
        LogEventLevel.Error => "ERR",
        _ => "FTL",
    };

    private static string Property(LogEvent logEvent, string name) =>
        logEvent.Properties.TryGetValue(name, out var value) ? value.ToString().Trim('"') : "?";

    /// <summary>The last two segments of a namespace-qualified type. The full name is usually longer than the
    /// message it prefixes, and the part that identifies the writer is at the end.</summary>
    private static string Shorten(string sourceContext)
    {
        var parts = sourceContext.Split('.');
        return parts.Length <= 2 ? sourceContext : string.Join('.', parts[^2..]);
    }
}
