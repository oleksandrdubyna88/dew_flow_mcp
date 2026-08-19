using System.Text.Json;
using FluentAssertions;
using Mcp.Contracts;
using Mcp.Telemetry;
using Xunit;

namespace Mcp.Tests;

/// <summary>Every field of the <c>telemetry/v0</c> wire shape, pinned by name on the EMITTER.
///
/// <para><b>Why every field and not a sample.</b> `dew_flow_benchmark` parses this format with its own
/// codec and never references this assembly, so a rename here compiles green on both sides while the
/// consumer starts reading zeroes — the exact mechanism of the documented `collapse`-stage incident
/// (testing.md, "A green suite is not evidence about a CROSS-REPOSITORY contract"). The 2026-08-16
/// audit counted 16 wire fields with roughly 6 asserted by name; this test closes that gap: the emitted
/// tree is ENUMERATED from a real serialized record — never retyped from the C# — and compared against
/// the pinned contract below. Changing the wire in any way, adding included, must touch this list, and
/// touching this list is the reminder that the consumer's codec and fixture move in the same change.</para>
/// </summary>
public sealed class TelemetryWireShapeTests
{
    /// <summary>The v0 contract, leaf by leaf. This list IS the wire format; edits to it are edits to
    /// the published schema and carry the consumer with them.</summary>
    private static readonly string[] V0Leaves =
    [
        "schema",
        "at",
        "emitter.app",
        "emitter.pid",
        "emitter.machine",
        "caller.clientName.captured",
        "caller.clientName.value",
        "caller.clientName.reason",
        "caller.clientVersion.captured",
        "caller.clientVersion.value",
        "caller.clientVersion.reason",
        "caller.model.captured",
        "caller.model.value",
        "caller.model.reason",
        "caller.transport",
        "correlation.leg.captured",
        "correlation.leg.value",
        "correlation.leg.reason",
        "correlation.phase.captured",
        "correlation.phase.value",
        "correlation.phase.reason",
        "tool",
        "scope",
        "argumentsJson",
        "argumentsTruncatedBytes",
        "outcome",
        "error",
        "responseChars",
        "responseBody",
        "responseTruncatedBytes",
        "tokens.captured",
        "tokens.value",
        "tokens.reason",
        "serverMs",
    ];

    [Fact]
    public void Every_field_of_the_v0_wire_tree_is_pinned_by_name()
    {
        var record = TelemetryRecord.From(
            SampleUsage(),
            new EmitterWire("mcp-test", 1234, "machine"),
            TelemetryCorrelation.Of("cell-17/verify"));

        var leaves = Leaves(JsonDocument.Parse(TelemetryJson.Line(record)).RootElement, prefix: string.Empty);

        // An exact SET in both directions: a renamed field shows up as one missing and one unexpected;
        // a new field shows up as unexpected until the contract list — and the consumer — learn it.
        leaves.Should().BeEquivalentTo(V0Leaves);
    }

    /// <summary>Every leaf path of the serialized record — read from the emitted JSON, so the test
    /// asserts what actually goes on the wire rather than what the C# declares about itself.</summary>
    private static IReadOnlyList<string> Leaves(JsonElement element, string prefix) =>
        element.ValueKind is JsonValueKind.Object
            ? [.. element.EnumerateObject()
                .SelectMany(p => Leaves(p.Value, prefix.Length == 0 ? p.Name : $"{prefix}.{p.Name}"))]
            : [prefix];

    /// <summary>Fully populated on purpose: a leaf the sample leaves at a serializer default is a leaf
    /// this test cannot see go missing.</summary>
    private static ToolUsage SampleUsage() =>
        new(
            "rt_read_local_file",
            new DateTimeOffset(2026, 8, 19, 12, 0, 0, TimeSpan.Zero),
            new CallerIdentity(
                Captured.Text("claude-code"),
                Captured.Text("2.0.0"),
                Captured.Unavailable("the MCP protocol carries no model identity for the caller"),
                "stdio"),
            "D:/work/repo",
            """{"path":"a.txt"}""",
            7,
            ToolOutcome.Answered,
            "an error text",
            42,
            "lines 1-3 of 3",
            9,
            CapturedCount.Number(1_234),
            TimeSpan.FromMilliseconds(13.4));
}
