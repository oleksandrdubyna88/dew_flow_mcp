using System.Collections.Frozen;
using Mcp.Contracts;

namespace Mcp.Application;

/// <summary>One provider, presented as the subset this server was configured to serve, carrying the
/// descriptions this server was configured to advertise.
/// <para>It sits AHEAD of <see cref="ToolCatalog"/> rather than inside it, and that placement is the
/// whole design. The catalog, <see cref="ToolSchema"/>, <see cref="IToolProvider"/> and both
/// presentations are untouched, so the parity guarantee between the protocol surface and the local-LLM
/// bridge holds by construction rather than by a second assertion: both still project the one
/// <see cref="ToolCatalog.Advertised"/> list they always did, and that list is simply now
/// configurable.</para>
/// <para><see cref="ToolSchema"/> is a record, so an override is <c>with { Description = … }</c> — no
/// mutation, no second schema shape to keep in step, and the argument schema is carried through
/// byte-identical.</para></summary>
internal sealed class ToolSurfaceProvider : IToolProvider
{
    private readonly IToolProvider inner;
    private readonly FrozenSet<string> served;

    internal ToolSurfaceProvider(
        IToolProvider inner,
        IReadOnlySet<string> allowed,
        ToolDescriptionCatalog descriptions)
    {
        this.inner = inner;
        Tools =
        [
            .. inner.Tools
                .Where(tool => allowed.Count == 0 || allowed.Contains(tool.Name))
                .Select(tool => tool with { Description = descriptions.DescriptionFor(tool.Name, tool.Description) }),
        ];
        served = Tools.Select(tool => tool.Name).ToFrozenSet(StringComparer.Ordinal);
    }

    public IReadOnlyList<ToolSchema> Tools { get; }

    public string Scope => inner.Scope;

    /// <summary>A tool outside the configured surface is REFUSED, not failed. It never appears in the
    /// advertised list, so a caller reaching for it is working from a stale configuration — the same
    /// reading a sandbox denial gets, and <see cref="ToolResult.Refused"/> is the state that says so.
    /// <para>The catalog routes by advertised name and so cannot reach here today; this holds the
    /// guarantee at the layer that owns it, for any future caller that resolves a provider directly.
    /// </para></summary>
    public Task<ToolResult> InvokeAsync(ToolCall call, CancellationToken cancellationToken) =>
        served.Contains(call.Name)
            ? inner.InvokeAsync(call, cancellationToken)
            : Task.FromResult(ToolResult.Refusal(
                $"'{call.Name}' is not part of this server's configured tool surface."));
}
