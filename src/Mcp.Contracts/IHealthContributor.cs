namespace Mcp.Contracts;

/// <summary>Something the health probe may ask whether it is still doing its job.
/// <para>A PORT for the same reason <see cref="IUsageSink"/> is one: the management surface must be
/// able to report a dead background writer without the API project ever learning what a spool is, and
/// a component added later must reach <c>/health</c> by implementing this and registering itself —
/// not by an edit inside the API.</para></summary>
public interface IHealthContributor
{
    /// <summary>Reads live state and returns IMMEDIATELY.
    /// <para>An implementation must take no lock another thread can hold, do no IO, and compute nothing
    /// lazily. A probe that blocks is how one slow component becomes an outage for every orchestrator
    /// polling it — and the probe is the thing that was supposed to reveal the problem.</para></summary>
    ComponentHealth Check();
}

/// <summary>One component's answer. <paramref name="Detail"/> carries the numbers behind the verdict:
/// a health endpoint that says only "degraded" has moved the diagnosis somewhere else instead of
/// answering it.</summary>
public sealed record ComponentHealth(string Component, bool Healthy, string Detail)
{
    public static ComponentHealth Alive(string component, string detail) => new(component, true, detail);

    public static ComponentHealth Broken(string component, string detail) => new(component, false, detail);
}
