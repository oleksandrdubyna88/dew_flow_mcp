using FluentAssertions;
using Mcp.Application;
using Mcp.Bridge;
using Mcp.Contracts;
using Xunit;

namespace Mcp.Tests;

/// <summary>Who is calling, and the limit of what may honestly be claimed about it.</summary>
public sealed class CallerIdentityTests
{
    [Fact]
    public void With_no_presentation_every_field_is_uncaptured_and_says_why()
    {
        var current = new AmbientCallerContext().Current;

        current.ClientName.WasCaptured.Should().BeFalse();
        current.Model.WasCaptured.Should().BeFalse();
        current.ClientName.Reason.Should().NotBeEmpty("an unknown must carry the reason it is unknown");
    }

    [Fact]
    public void A_presentation_establishes_the_caller_for_its_call_and_restores_the_previous_one()
    {
        var context = new AmbientCallerContext();
        var outer = BridgeCaller.Driving("qwen3-coder").Identity;
        var inner = BridgeCaller.Driving("opus").Identity;

        using (context.Enter(outer))
        {
            context.Current.Model.Value.Should().Be("qwen3-coder");

            using (context.Enter(inner))
            {
                context.Current.Model.Value.Should().Be("opus");
            }

            // A nested call must not leak its identity upward — the bridge can be driven from inside a
            // protocol call, and the outer call is still the outer caller's.
            context.Current.Model.Value.Should().Be("qwen3-coder");
        }

        context.Current.Model.WasCaptured.Should().BeFalse("the scope ended, so nobody is calling");
    }

    [Fact]
    public async Task Two_concurrent_calls_do_not_see_each_other_s_caller()
    {
        var context = new AmbientCallerContext();

        async Task<string> CallAs(string model, int delayMs)
        {
            using (context.Enter(BridgeCaller.Driving(model).Identity))
            {
                await Task.Delay(delayMs, TestContext.Current.CancellationToken);
                return context.Current.Model.Value;
            }
        }

        // One server serves many sessions at once. A single mutable slot would attribute whichever call
        // finished last to whoever asked first — the last-writer-wins defect a shared status string has.
        var results = await Task.WhenAll(CallAs("first", 40), CallAs("second", 5));

        results.Should().Equal("first", "second");
    }

    [Fact]
    public void The_bridge_records_its_model_when_the_host_names_one()
    {
        var caller = BridgeCaller.Driving("qwen3-coder").Identity;

        caller.Model.Should().Be(Captured.Text("qwen3-coder"));
        caller.Transport.Should().Be("in-process");
    }

    [Fact]
    public void An_unnamed_bridge_model_is_not_captured_rather_than_defaulted()
    {
        var caller = BridgeCaller.Driving(string.Empty).Identity;

        // The single most tempting field to guess in the whole record, and the one where a guess is
        // indistinguishable from a measurement afterwards.
        caller.Model.WasCaptured.Should().BeFalse();
        caller.Model.Value.Should().BeEmpty();
        caller.Model.Reason.Should().NotBeEmpty();
    }
}
