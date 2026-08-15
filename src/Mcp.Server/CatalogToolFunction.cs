using System.Text.Json;
using Mcp.Application;
using Mcp.Contracts;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Protocol;

namespace Mcp.Server;

/// <summary>The ONE adapter from a catalog entry to the protocol SDK's function shape.
/// <para>Deliberately generic: the SDK's usual route is an attributed method per tool, which would mean
/// writing every tool twice — once for the protocol and once for the local-LLM bridge. That is exactly
/// the duplication that produced two divergent surfaces before. One adapter, driven by
/// <see cref="ToolSchema"/>, means a tool is authored once and both presentations get it.</para></summary>
internal sealed class CatalogToolFunction(ToolSchema schema, ToolCatalog catalog) : AIFunction
{
    public override string Name => schema.Name;

    public override string Description => schema.Description;

    public override JsonElement JsonSchema => schema.InputSchema;

    protected override async ValueTask<object?> InvokeCoreAsync(
        AIFunctionArguments arguments,
        CancellationToken cancellationToken)
    {
        var call = new ToolCall(schema.Name, ArgumentsToJson(arguments));
        var result = await catalog.InvokeAsync(call, cancellationToken);

        // A refusal MUST reach the wire as isError, not as ordinary content: a caller that cannot tell
        // "refused" from "returned nothing" is the exact failure this union exists to prevent — a
        // sandbox denial would read as an empty file. Returning CallToolResult keeps it a value rather
        // than a thrown transport fault, so the model can read the reason and correct itself.
        //
        // Refused and Failed collapse to the same flag HERE, and only here: the protocol has one error
        // state, so the third case is a distinction this server keeps for itself (telemetry, and any
        // in-process consumer) rather than one it invents on the wire.
        return result.Match(
            ok => new CallToolResult { Content = [new TextContentBlock { Text = ok }] },
            refused => Error(refused),
            failed => Error(failed));
    }

    private static CallToolResult Error(string text) =>
        new() { IsError = true, Content = [new TextContentBlock { Text = text }] };

    private static JsonElement ArgumentsToJson(AIFunctionArguments arguments) =>
        JsonSerializer.SerializeToElement(
            arguments.ToDictionary(a => a.Key, a => a.Value),
            McpJson.Options);
}

/// <summary>Serialization settings shared by the protocol adapter, kept in one place so the argument
/// and result shapes cannot drift apart.</summary>
internal static class McpJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}
