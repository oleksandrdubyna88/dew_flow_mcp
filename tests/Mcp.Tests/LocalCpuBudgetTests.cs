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
                "<Content Include=\"xunit.runner.json\" CopyToOutputDirectory=\"PreserveNewest\" />",
                $"{project} must copy xunit.runner.json to its output, or the runner never reads it");
            CapIn(Path.Combine(Path.GetDirectoryName(project)!, "xunit.runner.json"))
                .Should().BeInRange(1, FamilyCap, $"{project} caps its test threads");
        }
    }

    [Fact]
    public void The_running_suite_reads_a_cap_of_eight_or_fewer()
    {
        // What THIS run was configured with: the file the runner reads, in the runner's own folder.
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

    [GeneratedRegex(@"\.Tests\.exe")]
    private static partial Regex TestExe();
}

/// <summary>The cap, observed rather than configured. Nine probe classes — each its own collection, so
/// xUnit may run them all at once — meet in one gauge: each waits up to half a second for company and then
/// checks the most that were ever inside together. With the cap honoured that is at most eight; if the
/// runner ignored its config, nine would meet and the probe that saw it goes red.</summary>
internal static class ConcurrencyGauge
{
    private static int _inside;
    private static int _peak;

    // Blocking on purpose: xUnit caps how many tests RUN at once, and a probe that awaited would hand its
    // thread back and let the runner start more, measuring the scheduler instead of the cap.
    public static int Meet()
    {
        var now = Interlocked.Increment(ref _inside);
        int seen;
        while ((seen = Volatile.Read(ref _peak)) < now && Interlocked.CompareExchange(ref _peak, now, seen) != seen) { }
        try
        {
            var until = DateTime.UtcNow.AddMilliseconds(500);
            while (DateTime.UtcNow < until && Volatile.Read(ref _inside) < 9) Thread.Sleep(10);
            var inside = Volatile.Read(ref _inside);
            while ((seen = Volatile.Read(ref _peak)) < inside && Interlocked.CompareExchange(ref _peak, inside, seen) != seen) { }
            return Volatile.Read(ref _peak);
        }
        finally
        {
            Interlocked.Decrement(ref _inside);
        }
    }
}

public sealed class ConcurrencyProbe1 { [Fact] public void Never_more_than_the_cap_at_once() => ConcurrencyGauge.Meet().Should().BeLessThanOrEqualTo(8); }
public sealed class ConcurrencyProbe2 { [Fact] public void Never_more_than_the_cap_at_once() => ConcurrencyGauge.Meet().Should().BeLessThanOrEqualTo(8); }
public sealed class ConcurrencyProbe3 { [Fact] public void Never_more_than_the_cap_at_once() => ConcurrencyGauge.Meet().Should().BeLessThanOrEqualTo(8); }
public sealed class ConcurrencyProbe4 { [Fact] public void Never_more_than_the_cap_at_once() => ConcurrencyGauge.Meet().Should().BeLessThanOrEqualTo(8); }
public sealed class ConcurrencyProbe5 { [Fact] public void Never_more_than_the_cap_at_once() => ConcurrencyGauge.Meet().Should().BeLessThanOrEqualTo(8); }
public sealed class ConcurrencyProbe6 { [Fact] public void Never_more_than_the_cap_at_once() => ConcurrencyGauge.Meet().Should().BeLessThanOrEqualTo(8); }
public sealed class ConcurrencyProbe7 { [Fact] public void Never_more_than_the_cap_at_once() => ConcurrencyGauge.Meet().Should().BeLessThanOrEqualTo(8); }
public sealed class ConcurrencyProbe8 { [Fact] public void Never_more_than_the_cap_at_once() => ConcurrencyGauge.Meet().Should().BeLessThanOrEqualTo(8); }
public sealed class ConcurrencyProbe9 { [Fact] public void Never_more_than_the_cap_at_once() => ConcurrencyGauge.Meet().Should().BeLessThanOrEqualTo(8); }
