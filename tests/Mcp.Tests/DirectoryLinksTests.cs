using System.Diagnostics;
using FluentAssertions;
using Mcp.Tests.Leftovers;
using Xunit;

namespace Mcp.Tests;

/// <summary>The helper that runs the junction tool: a program and its arguments, never a shell string, and a bound on how long it may take.</summary>
public sealed class DirectoryLinksTests : IDisposable
{
    private readonly OwnedScope _owned = new();

    public void Dispose() => _owned.Dispose();

    [Fact]
    public void A_tool_that_takes_too_long_is_killed_and_the_run_says_so()
    {
        if (OperatingSystem.IsWindows()) Assert.Skip("the stand-in is sleep(1)");
        var clock = Stopwatch.StartNew();

        var run = DirectoryLinks.Run("sleep", ["30"], TimeSpan.FromMilliseconds(300));

        run.TimedOut.Should().BeTrue();
        clock.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(15));
        Liveness.ProbeProcess(run.Pid).State.Should().Be(ProbeState.Dead, "the timed-out process was killed, not left running");
    }

    [Fact]
    public void An_argument_with_spaces_quotes_and_shell_syntax_reaches_the_tool_as_one_argument()
    {
        if (OperatingSystem.IsWindows()) Assert.Skip("the stand-in is sh(1)");
        var odd = "a b\"c'd; $(touch pwned) `x` & y";

        var run = DirectoryLinks.Run("sh", ["-c", "test \"$#\" -eq 1 && test \"$1\" = \"a b\\\"c'd; \\$(touch pwned) \\`x\\` & y\"", "sh", odd], TimeSpan.FromSeconds(30));

        run.TimedOut.Should().BeFalse();
        run.ExitCode.Should().Be(0);
        File.Exists(Path.Combine(Directory.GetCurrentDirectory(), "pwned")).Should().BeFalse();
    }

    // N2 (benchmark's E6.S2 code gate: "Run drains only stdout, after the exit"): a child that writes more to each pipe than the pipe holds
    // must finish on its own. A tool blocked on a full stderr pipe is only ever ended by the timeout kill, and that would read as a hang.
    [Fact]
    public void A_tool_that_writes_more_than_a_pipe_holds_to_both_streams_finishes_on_its_own_and_its_output_is_read_whole()
    {
        if (OperatingSystem.IsWindows()) Assert.Skip("the stand-in is sh(1)");
        const int bytes = 200_000; // a pipe holds 64 KiB on Linux
        var script = $"head -c {bytes} /dev/zero | tr '\\0' x >&2; head -c {bytes} /dev/zero | tr '\\0' y";

        var run = DirectoryLinks.Run("sh", ["-c", script], TimeSpan.FromSeconds(60));

        run.TimedOut.Should().BeFalse("the child must exit by itself, not be killed while blocked on a full pipe");
        run.ExitCode.Should().Be(0);
        run.Output.Should().HaveLength(bytes).And.MatchRegex("^y+$");
    }

    // N3: `cmd /c mklink /J` is given a path with a space as one argument (the runtime quotes it), so only the characters cmd reads as syntax
    // are blocked, and a space is not one. Windows only: there is no mklink elsewhere.
    [Fact]
    public void A_junction_is_made_under_a_link_path_that_holds_a_space()
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("mklink /J is Windows only");
        var root = _owned.Dir("spaced link");
        var target = Path.Combine(root, "the target");
        Directory.CreateDirectory(target);
        var link = Path.Combine(root, "the link");

        var made = DirectoryLinks.Run("cmd.exe", ["/c", "mklink", "/J", link, target], DirectoryLinks.ToolTimeout);

        made.TimedOut.Should().BeFalse();
        made.ExitCode.Should().Be(0, made.Output);
        Directory.Exists(link).Should().BeTrue();
        new DirectoryInfo(link).Attributes.Should().HaveFlag(FileAttributes.ReparsePoint, "it is a junction, not a copy");
    }
}
