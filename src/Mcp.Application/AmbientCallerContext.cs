using Mcp.Contracts;

namespace Mcp.Application;

/// <summary>Holds the caller of the call being served, for the duration of that call.
/// <para>
/// <see cref="AsyncLocal{T}"/> rather than a field: one server process serves many concurrent calls
/// from different sessions, and a single mutable slot would attribute whichever call finished last to
/// whoever asked first — the same last-writer-wins defect a shared status string has elsewhere.
/// </para></summary>
public sealed class AmbientCallerContext : ICallerContext
{
    private static readonly AsyncLocal<CallerIdentity?> Slot = new();

    private const string NoContext =
        "no presentation established a caller context for this call";

    public CallerIdentity Current => Slot.Value ?? CallerIdentity.NotCaptured(NoContext);

    /// <summary>Establishes the caller for the current asynchronous flow, restoring the previous one on
    /// dispose so a nested call (a bridge invoked from inside a protocol call) cannot leak its identity
    /// upward.</summary>
    public IDisposable Enter(CallerIdentity identity)
    {
        var previous = Slot.Value;
        Slot.Value = identity;
        return new Scope(previous);
    }

    private sealed class Scope(CallerIdentity? previous) : IDisposable
    {
        public void Dispose() => Slot.Value = previous;
    }
}
