using System.Collections.Frozen;

namespace Mcp.Application;

/// <summary>Which tools this process serves, and where their descriptions come from.
/// <para>Process-start configuration covers both shapes of host this repository ships: the stdio
/// server is a subprocess per client, and the HTTP server is configured per deployment. A per-session
/// or per-call catalog is a large mechanism for a case a restart already answers.</para>
/// <para>Every member defaults to "nothing configured", so a host that says nothing gets exactly
/// today's behaviour — the same tools, and the descriptions compiled into their providers.</para>
/// </summary>
public sealed record ToolSurfaceOptions
{
    /// <summary>The tools to advertise. <b>Empty means every tool</b> — the default, and the state that
    /// leaves the shipped surface untouched.</summary>
    public IReadOnlySet<string> Tools { get; init; } = FrozenSet<string>.Empty;

    /// <summary>Where description files live. Empty leaves every description as the literal compiled
    /// into its provider.</summary>
    public string DescriptionsDirectory { get; init; } = string.Empty;

    /// <summary>Which subfolder of that directory to read. Empty reads the directory itself.</summary>
    public string DescriptionSet { get; init; } = string.Empty;

    /// <summary>The shipped surface: every registered tool, every compiled description.</summary>
    public static ToolSurfaceOptions Everything { get; } = new();

    /// <summary>Nothing was configured, so no decorator is applied at all and the catalog is built from
    /// the registered providers exactly as it always was.</summary>
    public bool IsEverything => Tools.Count == 0 && DescriptionsDirectory.Length == 0;

    /// <summary>Builds a surface from the three strings a host reads off its command line. The only
    /// parsing here is the tool list, which is comma-separated because a repeated flag is a shape the
    /// host's one-value <c>ReadOption</c> helper does not have and does not need.</summary>
    public static ToolSurfaceOptions From(string tools, string descriptionsDirectory, string descriptionSet) =>
        new()
        {
            Tools = tools
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToFrozenSet(StringComparer.Ordinal),
            DescriptionsDirectory = descriptionsDirectory.Trim(),
            DescriptionSet = descriptionSet.Trim(),
        };
}
