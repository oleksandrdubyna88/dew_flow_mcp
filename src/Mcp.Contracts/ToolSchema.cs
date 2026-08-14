using System.Text.Json;

namespace Mcp.Contracts;

/// <summary>What a tool is called, what it does, and the JSON schema of its arguments.
/// This is the PUBLISHED contract: a customer's Claude Code / Codex / Gemini configuration depends on
/// these names and shapes, so a change here is a versioned change, never a silent edit.</summary>
public sealed record ToolSchema
{
    public required string Name { get; init; }

    public required string Description { get; init; }

    /// <summary>JSON Schema of the argument object, as the protocol wants it.</summary>
    public required JsonElement InputSchema { get; init; }

    /// <summary>Whether invoking this tool can change anything the caller owns. Read-only is the
    /// default because a surface that guesses wrong in this direction is the dangerous one.</summary>
    public ToolTrait Trait { get; init; } = ToolTrait.ReadOnly;

    /// <summary>Parses a schema written as JSON text. Throws on malformed input by design: a broken
    /// schema is a programming error found at startup, not an expected runtime failure.</summary>
    public static JsonElement ParseSchema(string json) =>
        JsonDocument.Parse(json).RootElement.Clone();
}

/// <summary>Whether a tool only reads, or can write.</summary>
public enum ToolTrait
{
    ReadOnly,
    Mutating,
}
