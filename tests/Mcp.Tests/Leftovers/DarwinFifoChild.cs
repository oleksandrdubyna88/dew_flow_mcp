using System.Globalization;
using FluentAssertions;
using Xunit;

namespace Mcp.Tests.Leftovers;

/// <summary>The CHILD half of the FIFO test of <see cref="DarwinNativeTests"/>: this test runs only when that test starts the executable
/// again and hands it a claim path and the identity judged. It does the one read that would hang for ever if the open waited for a writer —
/// in a process of its own, so the parent can bound it and kill it. Normally it skips.</summary>
public sealed class DarwinFifoChild
{
    /// <summary>The variable that makes the child's test run: <c>&lt;path&gt;|&lt;dev&gt;|&lt;ino&gt;</c> of the claim, as judged before the swap.</summary>
    internal const string Variable = "MCP_DARWIN_FIFO_CHILD";

    [Fact]
    public void Reads_a_claim_that_is_now_a_fifo_and_is_refused_without_waiting()
    {
        var spec = Environment.GetEnvironmentVariable(Variable);
        if (string.IsNullOrEmpty(spec)) Assert.Skip($"{Variable} is not set — this test is the child of DarwinNativeTests and runs only under it");

        var parts = spec.Split('|');
        var judged = new Identity(
            ulong.Parse(parts[1], CultureInfo.InvariantCulture), UInt128.Parse(parts[2], CultureInfo.InvariantCulture),
            IsDirectory: false, IsLink: false, IsRegular: true, Uid: null, default, default);

        var bytes = Native.ReadVerifiedRegular(parts[0], judged, 513);

        bytes.Should().BeNull("a FIFO is refused by its type, after an open that did not wait for a writer");
    }
}
