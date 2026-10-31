using Xunit;

[assembly: AssemblyFixture(typeof(Mcp.Tests.Leftovers.LeftoversSweepFixture))]

namespace Mcp.Tests.Leftovers;

/// <summary>The start-of-run sweep, as an assembly fixture: xUnit v3 builds it before any test of the assembly runs, so a
/// killed run's leftovers are collected before this run makes its own. It sweeps the REAL temp root, keeps what it did in
/// <see cref="LastRun"/>, and says something only when there is something to say. It never fails the run: the constructor
/// does not throw (rule §4) — a sweep error is a reported line, and the exit code is the tests'.</summary>
public sealed class LeftoversSweepFixture
{
    public LeftoversSweepFixture()
        : this(RealSweep, Console.Error.WriteLine)
    {
    }

    /// <summary>The seam: the sweep to run and where its report goes (no test is running yet when the real one builds).</summary>
    internal LeftoversSweepFixture(Func<SweepResult> sweep, Action<string> report)
    {
        SweepResult run;
        try
        {
            run = sweep();
        }
        catch (Exception ex)
        {
            Say(report, $"{Header} — error: sweep failed: {ex.Message.Split('\n')[0].TrimEnd('\r')}");
            LastRun = SweepResult.Empty;
            return;
        }

        LastRun = run;
        if (run.Count == 0) return;
        Say(report, $"{Header} — removed {run.Removed.Count}, preserved {run.Preserved.Count}, listed {run.Listed.Count}");
        foreach (var line in run.Listed) Say(report, "  " + line);
        foreach (var error in run.Errors) Say(report, "  error: " + error);
    }

    /// <summary>What the sweep of this run did; empty when it threw.</summary>
    public SweepResult LastRun { get; }

    private static string Header => $"{OwnedToken.RepoToken} tests: leftovers";

    private static SweepResult RealSweep() =>
        LeftoversSweep.Run(new SweepOptions(Owned.TempRoot(), DateTimeOffset.UtcNow, LeftoversSweep.MinAge, Liveness.ProbeProcess, Scope.IsMine));

    private static void Say(Action<string> report, string line)
    {
        try
        {
            report(line);
        }
        catch (Exception)
        {
            // A report that cannot be written is not a reason to fail the run.
        }
    }
}
