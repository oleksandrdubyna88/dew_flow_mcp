namespace Mcp.Contracts;

/// <summary>Something that contributes tools. THE seam of the whole product: this repository hosts
/// tools and knows nothing about who provides them, so retrieval, editing and any future capability
/// plug in by implementing this — the dependency arrow points at MCP, never away from it.
/// <para>Adding a capability is a new provider and zero edits inside the MCP module. That is also what
/// makes a second, drifting implementation of the same tool impossible to build.</para></summary>
public interface IToolProvider
{
    /// <summary>The tools this provider advertises. Read once when the catalog is built.</summary>
    IReadOnlyList<ToolSchema> Tools { get; }

    /// <summary>What this provider's tools are scoped to — a workspace root, a project, an index.
    /// Recorded with every call, because "which tool, how often" is a much weaker question than
    /// "against what". Defaulted so a provider that has no meaningful scope says nothing rather than
    /// inventing one.</summary>
    string Scope => string.Empty;

    /// <summary>Executes one of <see cref="Tools"/>. Expected failures come back as
    /// <see cref="ToolResult.Failed"/>; only genuinely unexpected infrastructure faults throw.</summary>
    Task<ToolResult> InvokeAsync(ToolCall call, CancellationToken cancellationToken);
}
