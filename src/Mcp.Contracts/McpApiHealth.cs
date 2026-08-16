namespace Mcp.Contracts;

/// <summary>Liveness answer for the management surface: one verdict, and the components it was
/// computed from.
/// <para>A degraded server still answers <b>200</b>, deliberately. The status code says the process is
/// serving; the body says how well. A 503 for a broken spool would tell a supervisor to restart a
/// server that is answering every tool call correctly — and the restart would lose more than the
/// telemetry did.</para>
/// <para>It lives in the contracts rather than beside its endpoint because two projects need it and
/// neither may reference the other: <c>Mcp.Api</c> produces it, and <c>Mcp.Ui</c> — a WASM-compatible
/// Razor library that must never pull in ASP.NET hosting — reads it. The shared half belongs in their
/// common ancestor rather than being written twice and left to drift.</para></summary>
public sealed record McpApiHealth(string Status, IReadOnlyList<ComponentHealth> Components)
{
    public const string Ok = "ok";

    public const string Degraded = "degraded";

    /// <summary>Nothing has been asked yet — which is not the same as a healthy server, and must not
    /// render as one.</summary>
    public static McpApiHealth Unknown { get; } = new(string.Empty, []);

    /// <summary>Asks every contributor and folds the answers into one verdict. No component means
    /// nothing to report, not a claim that anything was checked — which is why the components travel
    /// with the status instead of being summarised away.</summary>
    public static McpApiHealth From(IEnumerable<IHealthContributor> contributors)
    {
        var components = contributors.Select(c => c.Check()).ToList();
        return new McpApiHealth(components.All(c => c.Healthy) ? Ok : Degraded, components);
    }
}
