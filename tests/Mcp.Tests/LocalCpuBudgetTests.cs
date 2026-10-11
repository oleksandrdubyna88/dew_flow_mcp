using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace Mcp.Tests;

/// <summary>Local test runs take at most 8 threads and run at lowered CPU priority — the family rule
/// <c>common.local-cpu-budget</c> (<c>.claude/rules/shared/common/local-cpu-budget.md</c>). Measured
/// 2026-10-10 on a 24-thread workstation running several agent sessions: a runner left at its default
/// takes one worker per CPU and holds the machine at 100 % while everything else lags.
///
/// <para>The cap lives in <c>xunit.runner.json</c>, and the file only counts when it reaches the output
/// folder. Measured 2026-10-11 on xunit.v3 4.0.1 with thirty sleeping test classes recording their peak:
/// no file, 24 at once; a file beside the project but not copied, still 24; copied, 8.</para></summary>
public sealed partial class LocalCpuBudgetTests
{
    private const int FamilyCap = 8;

    private static readonly string Root = FindRoot();

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && dir.GetFiles("*.slnx").Length == 0) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException($"no .slnx above {AppContext.BaseDirectory}");
    }

    private static int CapIn(string jsonPath)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(jsonPath));
        return doc.RootElement.GetProperty("maxParallelThreads").GetInt32();
    }

    private static IEnumerable<string> TestProjects() =>
        Directory.EnumerateFiles(Path.Combine(Root, "tests"), "*.Tests.csproj", SearchOption.AllDirectories)
            .Where(p => !p.Split(Path.DirectorySeparatorChar).Any(part => part is "bin" or "obj"));

    [Fact]
    public void Every_test_project_caps_its_threads_at_eight_and_copies_the_config_to_its_output()
    {
        var projects = TestProjects().ToList();
        projects.Should().NotBeEmpty();
        foreach (var project in projects)
        {
            File.ReadAllText(project).Should().Contain(
                "<Content Include=\"xunit.runner.json\" CopyToOutputDirectory=\"PreserveNewest\" Condition=\"'$(CI)' != 'true'\" />",
                $"{project} must copy xunit.runner.json to its output for local runs (not on CI, where an exact 8 would RAISE a small runner)");
            CapIn(Path.Combine(Path.GetDirectoryName(project)!, "xunit.runner.json"))
                .Should().BeInRange(1, FamilyCap, $"{project} caps its test threads");
        }
    }

    [Fact]
    public void The_running_suite_reads_a_cap_of_eight_or_fewer()
    {
        // What THIS run was configured with: the file the runner reads, in the runner's own folder. A CI build
        // does not copy it on purpose (CI is exempt, and an exact 8 would raise a 4-vCPU runner).
        if (IsCi()) Assert.Skip("CI builds do not copy the cap; CI keeps its own thread count");
        var copied = Path.Combine(AppContext.BaseDirectory, "xunit.runner.json");
        File.Exists(copied).Should().BeTrue("the cap only counts once it reaches the output folder");
        CapIn(copied).Should().BeInRange(1, FamilyCap);
    }

    [Fact]
    public void The_documented_test_commands_run_lowered()
    {
        var lines = new[] { "CLAUDE.md", "README.md" }
            .Select(f => Path.Combine(Root, f))
            .Where(File.Exists)
            .SelectMany(File.ReadAllLines)
            .Where(line => TestExe().IsMatch(line))
            .ToList();
        lines.Should().NotBeEmpty("the scan must still find the documented test executable, or it proves nothing");
        lines.Should().OnlyContain(line => line.Contains("low-priority.mjs"),
            "a documented test run goes through the mount's low-priority.mjs");
    }

    private static bool IsCi() =>
        Environment.GetEnvironmentVariable("CI") is { Length: > 0 } ci && !ci.Equals("false", StringComparison.OrdinalIgnoreCase) && ci != "0";

    [GeneratedRegex(@"\.Tests\.exe")]
    private static partial Regex TestExe();
}

/// <summary>The cap, observed rather than configured. Nine probe classes — each its own collection, so
/// xUnit may run them all at once — meet in one gauge: each waits up to half a second for company and then
/// checks the most that were ever inside together. With the cap honoured that is at most eight; if the
/// runner ignored its config, nine would meet and the probe that saw it goes red.</summary>
internal static class ConcurrencyGauge
{
    private const int Probes = 9;
    private static int _inside;
    private static int _peak;

    /// <summary>The probe's assertion. Skipped when the run was started with an explicit max-threads
    /// switch: that is a deliberate override the rule allows, and a red probe would report the person's
    /// choice as a defect.</summary>
    public static void AssertNeverMoreThanTheCap()
    {
        if (Environment.GetCommandLineArgs().Any(arg => arg.StartsWith("--max-threads", StringComparison.Ordinal)))
            Assert.Skip("the run set its own max-threads; the default cap is not what is being run");
        Meet().Should().BeLessThanOrEqualTo(8, "the runner must never run more tests at once than its cap");
    }

    // Blocking on purpose: xUnit caps how many tests RUN at once, and a probe that awaited would hand its
    // thread back and let the runner start more, measuring the scheduler instead of the cap.
    private static int Meet()
    {
        RaisePeakTo(Interlocked.Increment(ref _inside));
        try
        {
            WaitForCompany();
            RaisePeakTo(Volatile.Read(ref _inside));
            return Volatile.Read(ref _peak);
        }
        finally
        {
            Interlocked.Decrement(ref _inside);
        }
    }

    private static void WaitForCompany()
    {
        var until = DateTime.UtcNow.AddMilliseconds(500);
        while (DateTime.UtcNow < until && Volatile.Read(ref _inside) < Probes) Thread.Sleep(10);
    }

    private static void RaisePeakTo(int value)
    {
        int seen;
        do { seen = Volatile.Read(ref _peak); }
        while (seen < value && Interlocked.CompareExchange(ref _peak, value, seen) != seen);
    }
}

public sealed class ConcurrencyProbe1 { [Fact] public void Never_more_than_the_cap_at_once() => ConcurrencyGauge.AssertNeverMoreThanTheCap(); }
public sealed class ConcurrencyProbe2 { [Fact] public void Never_more_than_the_cap_at_once() => ConcurrencyGauge.AssertNeverMoreThanTheCap(); }
public sealed class ConcurrencyProbe3 { [Fact] public void Never_more_than_the_cap_at_once() => ConcurrencyGauge.AssertNeverMoreThanTheCap(); }
public sealed class ConcurrencyProbe4 { [Fact] public void Never_more_than_the_cap_at_once() => ConcurrencyGauge.AssertNeverMoreThanTheCap(); }
public sealed class ConcurrencyProbe5 { [Fact] public void Never_more_than_the_cap_at_once() => ConcurrencyGauge.AssertNeverMoreThanTheCap(); }
public sealed class ConcurrencyProbe6 { [Fact] public void Never_more_than_the_cap_at_once() => ConcurrencyGauge.AssertNeverMoreThanTheCap(); }
public sealed class ConcurrencyProbe7 { [Fact] public void Never_more_than_the_cap_at_once() => ConcurrencyGauge.AssertNeverMoreThanTheCap(); }
public sealed class ConcurrencyProbe8 { [Fact] public void Never_more_than_the_cap_at_once() => ConcurrencyGauge.AssertNeverMoreThanTheCap(); }
public sealed class ConcurrencyProbe9 { [Fact] public void Never_more_than_the_cap_at_once() => ConcurrencyGauge.AssertNeverMoreThanTheCap(); }
