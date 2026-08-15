using System.Text.Json;
using System.Text.Json.Serialization;
using Mcp.Contracts;

namespace Mcp.Telemetry;

/// <summary>One tool call as it goes on the wire — the emitter's view of the <c>telemetry/v0</c>
/// schema, which is OWNED by the benchmark that ingests it.
/// <para>
/// Versioned from v0 and stamped on every line: a consumer that meets a version it does not know must
/// be able to refuse that line by name rather than mis-read it. A schema that can change without
/// saying so is the same defect as a suite version that can be edited in place.
/// </para></summary>
public sealed record TelemetryRecord(
    [property: JsonPropertyName("schema")] string Schema,
    [property: JsonPropertyName("at")] DateTimeOffset At,
    [property: JsonPropertyName("emitter")] EmitterWire Emitter,
    [property: JsonPropertyName("caller")] CallerWire Caller,
    [property: JsonPropertyName("tool")] string Tool,
    [property: JsonPropertyName("scope")] string Scope,
    [property: JsonPropertyName("argumentsJson")] string ArgumentsJson,
    [property: JsonPropertyName("argumentsTruncatedBytes")] int ArgumentsTruncatedBytes,
    [property: JsonPropertyName("outcome")] string Outcome,
    [property: JsonPropertyName("error")] string Error,
    [property: JsonPropertyName("responseChars")] int ResponseChars,
    [property: JsonPropertyName("responseBody")] string ResponseBody,
    [property: JsonPropertyName("responseTruncatedBytes")] int ResponseTruncatedBytes,
    [property: JsonPropertyName("tokens")] CapturedNumberWire Tokens,
    [property: JsonPropertyName("serverMs")] double ServerMs)
{
    public const string V0 = "telemetry/v0";

    public static TelemetryRecord From(ToolUsage usage, EmitterWire emitter) =>
        new(
            V0,
            usage.At,
            emitter,
            CallerWire.From(usage.Caller),
            usage.ToolName,
            usage.Scope,
            usage.ArgumentsJson,
            usage.ArgumentsTruncatedBytes,
            Name(usage.Outcome),
            usage.Error,
            usage.ResponseChars,
            usage.ResponseBody,
            usage.ResponseTruncatedBytes,
            CapturedNumberWire.From(usage.Tokens),
            usage.Duration.TotalMilliseconds);

    /// <summary>Lower-case names on the wire, written out rather than taken from
    /// <see cref="Enum.ToString()"/>: the three values are a published vocabulary, and renaming the C#
    /// enum must not silently rename them.</summary>
    private static string Name(ToolOutcome outcome) => outcome switch
    {
        ToolOutcome.Answered => "answered",
        ToolOutcome.Refused => "refused",
        ToolOutcome.Error => "error",
        _ => "error",
    };
}

/// <summary>Which process wrote this line. Not decoration: one machine runs several of these hosts and
/// an aggregate that cannot separate them blends two populations into one row.</summary>
public sealed record EmitterWire(
    [property: JsonPropertyName("app")] string App,
    [property: JsonPropertyName("pid")] int Pid,
    [property: JsonPropertyName("machine")] string Machine);

public sealed record CallerWire(
    [property: JsonPropertyName("clientName")] CapturedTextWire ClientName,
    [property: JsonPropertyName("clientVersion")] CapturedTextWire ClientVersion,
    [property: JsonPropertyName("model")] CapturedTextWire Model,
    [property: JsonPropertyName("transport")] string Transport)
{
    public static CallerWire From(CallerIdentity caller) =>
        new(
            CapturedTextWire.From(caller.ClientName),
            CapturedTextWire.From(caller.ClientVersion),
            CapturedTextWire.From(caller.Model),
            caller.Transport);
}

/// <summary>A value that may not have been obtainable. The <c>captured</c> flag ships even when true,
/// so a consumer never has to infer the difference between "empty" and "unknown" from an absence.</summary>
public sealed record CapturedTextWire(
    [property: JsonPropertyName("captured")] bool Captured,
    [property: JsonPropertyName("value")] string Value,
    [property: JsonPropertyName("reason")] string Reason)
{
    public static CapturedTextWire From(Captured captured) =>
        new(captured.WasCaptured, captured.Value, captured.Reason);
}

public sealed record CapturedNumberWire(
    [property: JsonPropertyName("captured")] bool Captured,
    [property: JsonPropertyName("value")] long Value,
    [property: JsonPropertyName("reason")] string Reason)
{
    public static CapturedNumberWire From(CapturedCount count) =>
        new(count.WasCaptured, count.Value, count.Reason);
}

/// <summary>The one serializer for the spool. Kept here so the emitted bytes have a single definition
/// and a test can assert them.</summary>
public static class TelemetryJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        // One record per LINE, so a partially written file loses one call rather than the file.
        WriteIndented = false,
    };

    public static string Line(TelemetryRecord record) => JsonSerializer.Serialize(record, Options);
}
