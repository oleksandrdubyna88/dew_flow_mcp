using System.Text.Json;
using Mcp.Contracts;

namespace Workspace.Application;

/// <summary>The reference provider, and the proof that <see cref="IToolProvider"/> carries a real
/// capability rather than an interface nobody implements. One tool for now — the tool surface is
/// expected to change substantially, so this exists to exercise the MECHANISM end to end (schema,
/// dispatch, sandbox refusal, both presentations), not to be the final catalog.</summary>
public sealed class WorkspaceToolProvider(ISandboxedFileReader reader) : IToolProvider
{
    public const string ReadLocalFile = "rt_read_local_file";

    public IReadOnlyList<ToolSchema> Tools { get; } =
    [
        new ToolSchema
        {
            Name = ReadLocalFile,
            Description =
                "Read a file from the workspace, whole or as a line window. "
                + "startLine is 1-based inclusive; omit lineCount to read to the end. "
                + "Every result reports startLine/endLine/totalLines, so paging is a number, not a guess.",
            InputSchema = ToolSchema.ParseSchema(
                """
                {
                  "type": "object",
                  "properties": {
                    "path": { "type": "string", "description": "Workspace-relative path to read." },
                    "startLine": { "type": "integer", "description": "1-based first line. Omit to start at the top." },
                    "lineCount": { "type": "integer", "description": "How many lines to return. Omit to read to the end." }
                  },
                  "required": ["path"]
                }
                """),
            Trait = ToolTrait.ReadOnly,
        },
    ];

    /// <summary>Which workspace these reads are confined to — the answer to "a file was read, but
    /// where".</summary>
    public string Scope => reader.Sandbox;

    public async Task<ToolResult> InvokeAsync(ToolCall call, CancellationToken cancellationToken)
    {
        if (call.Name != ReadLocalFile)
        {
            return ToolResult.Failure($"'{call.Name}' is not served by {nameof(WorkspaceToolProvider)}.");
        }

        // A missing argument is the CALLER being told no, not this tool breaking — the same reading a
        // sandbox denial gets below.
        if (!TryReadPath(call.Arguments, out var path))
        {
            return ToolResult.Refusal("Argument 'path' is required.");
        }

        var request = new FileReadRequest(path, ReadInt(call.Arguments, "startLine"), ReadInt(call.Arguments, "lineCount"));
        var outcome = await reader.ReadAsync(request, cancellationToken);

        return outcome switch
        {
            FileReadOutcome.Ok ok => ToolResult.Success(Describe(ok)),
            // A sandbox denial is a REFUSAL, not a failure: the tool understood the request and
            // answered "no". Recording it as an error would put a working guard in the same bucket as
            // a broken disk, and the guard is the thing anyone auditing this surface wants to count.
            FileReadOutcome.Refused refused => ToolResult.Refusal(refused.Reason),
            _ => throw new InvalidOperationException("unreachable"),
        };
    }

    private static string Describe(FileReadOutcome.Ok ok) =>
        $"lines {ok.StartLine}-{ok.EndLine} of {ok.TotalLines}\n{ok.Content}";

    private static bool TryReadPath(JsonElement arguments, out string path)
    {
        path = string.Empty;
        if (arguments.ValueKind != JsonValueKind.Object
            || !arguments.TryGetProperty("path", out var value)
            || value.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        path = value.GetString() ?? string.Empty;
        return path.Length > 0;
    }

    private static int ReadInt(JsonElement arguments, string name) =>
        arguments.ValueKind == JsonValueKind.Object
        && arguments.TryGetProperty(name, out var value)
        && value.TryGetInt32(out var number)
            ? number
            : 0;
}
