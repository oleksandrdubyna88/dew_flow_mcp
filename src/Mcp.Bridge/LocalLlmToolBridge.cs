using System.Text.Json;
using System.Text.Json.Serialization;
using Mcp.Application;
using Mcp.Contracts;

namespace Mcp.Bridge;

/// <summary>Serves the catalog to a local model in-process, in the OpenAI-compatible function-calling
/// shape most local runtimes expect. No MCP protocol framing, no HTTP hop.
/// <para>It declares no tool of its own: <see cref="ToolDefinitions"/> is a projection of
/// <see cref="ToolCatalog.Advertised"/> and <see cref="InvokeAsync"/> forwards to the same catalog, so
/// the bridge and the protocol server cannot advertise different surfaces.</para></summary>
public sealed class LocalLlmToolBridge(ToolCatalog catalog, AmbientCallerContext callers, BridgeCaller caller)
{
    /// <summary>The catalog rendered as function definitions for a chat-completions request.</summary>
    public IReadOnlyList<LocalLlmToolDefinition> ToolDefinitions =>
        [.. catalog.Advertised.Select(t => new LocalLlmToolDefinition(
            new LocalLlmFunction(t.Name, t.Description, t.InputSchema)))];

    /// <summary>Runs one tool call requested by the model.
    /// <para>This is the one presentation whose caller identity is fully knowable: the host drives the
    /// model in-process, so it can say which one — and a benchmark leg that declares its model is the
    /// reason the telemetry's model column is ever populated at all.</para></summary>
    public async Task<ToolResult> InvokeAsync(string name, JsonElement arguments, CancellationToken cancellationToken)
    {
        using (callers.Enter(caller.Identity))
        {
            return await catalog.InvokeAsync(new ToolCall(name, arguments), cancellationToken);
        }
    }

    /// <summary>Runs a tool call whose arguments arrived as the JSON STRING these APIs send.
    /// Malformed JSON is the model's mistake, so it comes back as a tool failure the model can read and
    /// retry — never an exception that would abort the loop.</summary>
    public async Task<ToolResult> InvokeAsync(string name, string argumentsJson, CancellationToken cancellationToken)
    {
        JsonElement arguments;
        try
        {
            arguments = JsonDocument.Parse(
                string.IsNullOrWhiteSpace(argumentsJson) ? "{}" : argumentsJson).RootElement.Clone();
        }
        catch (JsonException ex)
        {
            return ToolResult.Failure($"Arguments for '{name}' are not valid JSON: {ex.Message}");
        }

        return await InvokeAsync(name, arguments, cancellationToken);
    }
}

/// <summary>One entry of the request's <c>tools</c> array.</summary>
public sealed record LocalLlmToolDefinition(
    [property: JsonPropertyName("function")] LocalLlmFunction Function)
{
    [JsonPropertyName("type")]
    public string Type => "function";
}

/// <summary>The function half of a tool definition — the same name, description and schema the protocol
/// surface advertises.</summary>
public sealed record LocalLlmFunction(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("parameters")] JsonElement Parameters);
