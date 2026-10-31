using System.Diagnostics;
using FluentAssertions;
using Mcp.Tests.Leftovers;
using Xunit;

namespace Mcp.Tests;

/// <summary>The helper that runs the junction tool: a program and its arguments, never a shell string, and a bound on how long it may take.</summary>
public sealed class DirectoryLinksTests
{
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
}
