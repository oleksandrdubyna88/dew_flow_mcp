namespace Mcp.Contracts;

/// <summary>Who is on the other end of a tool call, to the limit of what the transport actually knows.
/// <para>
/// The client application IS knowable — the protocol carries its name and version — and this server
/// simply never read it before. The MODEL is not: no MCP revision tells a server which model is
/// driving the session, so for a real session that field is <see cref="Captured.Unavailable"/> with
/// the reason, and it must never be guessed or defaulted to the popular answer. An in-process caller
/// that does know its model (a benchmark leg driving the bridge) declares it.
/// </para></summary>
public sealed record CallerIdentity(
    Captured ClientName,
    Captured ClientVersion,
    Captured Model,
    string Transport)
{
    /// <summary>No caller context was established at all — a direct catalog call, or a presentation
    /// that has not been taught to fill this in. Distinguishable from "the transport told us nothing".</summary>
    public static CallerIdentity NotCaptured(string reason) =>
        new(Captured.Unavailable(reason), Captured.Unavailable(reason), Captured.Unavailable(reason), string.Empty);
}

/// <summary>The caller of the call currently being served. Ambient rather than a parameter on
/// <see cref="ToolCall"/>: identity is established by the PRESENTATION (protocol session, in-process
/// bridge), and threading it through the tool contract would make every provider carry a field none
/// of them use.</summary>
public interface ICallerContext
{
    CallerIdentity Current { get; }
}
