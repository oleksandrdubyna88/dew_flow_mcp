using System.Collections.Frozen;

namespace Mcp.Application;

/// <summary>Tool descriptions resolved from files on disk, with the compiled literal as the floor.
/// <para>An agent picks a tool from its name, its description and its schema, and gets exactly one
/// chance. Measured on the previous generation: rewriting ONE instruction about which tool to use when
/// moved a score 16.5 points of 63, while swapping the toolbox from 4 tools to 18 moved it 1. So the
/// wording is a measured artefact — and a wording that can only change by rebuilding the binary is one
/// nobody will ever measure ten of.</para>
/// <para>Layout is <c>&lt;directory&gt;/&lt;set&gt;/&lt;tool-name&gt;.md</c>; an unnamed set reads the
/// directory itself. Everything is read ONCE, here, because a description is part of the published
/// contract for the life of a process: one that changed mid-session would make one session's traffic
/// two populations. Re-reading is a restart, which for a subprocess-per-task client is free.</para>
/// </summary>
public sealed class ToolDescriptionCatalog
{
    private const string FilePattern = "*.md";

    private readonly FrozenDictionary<string, string> byToolName;

    private ToolDescriptionCatalog(
        string set,
        FrozenDictionary<string, string> byToolName,
        IReadOnlyList<string> ignored)
    {
        Set = set;
        this.byToolName = byToolName;
        Ignored = ignored;
        NamedTools = byToolName.Keys.ToFrozenSet(StringComparer.Ordinal);
    }

    /// <summary>No catalog at all: every tool keeps the description compiled into its provider. This is
    /// what a host that names no directory gets — today's binary, byte for byte.</summary>
    public static ToolDescriptionCatalog None { get; } =
        new(string.Empty, FrozenDictionary<string, string>.Empty, []);

    /// <summary>Which set was loaded. Empty when the directory itself was read.</summary>
    public string Set { get; }

    /// <summary>The tools this catalog carries text for. Startup checks it against the tools the server
    /// actually serves, so a file named for a tool nobody offers stops the host instead of being an
    /// override that silently does nothing.</summary>
    public IReadOnlySet<string> NamedTools { get; }

    /// <summary>Files that were present and could not be used — blank, or unreadable — each with its
    /// reason. They fall back to the compiled literal as the never-empty rule requires, and the reason
    /// travels with them rather than being dropped: a description somebody wrote and this server
    /// ignored is exactly the silence this catalog exists to end.</summary>
    public IReadOnlyList<string> Ignored { get; }

    /// <summary>How to name this catalog in a startup message.</summary>
    public string Label => Set.Length > 0 ? $"description set '{Set}'" : "description directory";

    /// <summary>Reads every description file of one set. An unknown set is refused by name rather than
    /// silently serving the defaults — a surface quietly identical to the shipped one is the failure
    /// this whole seam exists to make visible.</summary>
    public static ToolDescriptionCatalog Load(string directory, string set)
    {
        if (directory.Length == 0)
        {
            return None;
        }

        var root = Path.Combine(directory, set);
        GuardAgainstAnUnknownSet(directory, set, root);

        var read = Directory
            .EnumerateFiles(root, FilePattern)
            .OrderBy(file => file, StringComparer.Ordinal)
            .Select(file => (Tool: Path.GetFileNameWithoutExtension(file.AsSpan()).ToString(), Outcome: ReadText(file)))
            .ToList();

        return new ToolDescriptionCatalog(
            set,
            read.Where(entry => entry.Outcome.Text.Length > 0)
                .ToFrozenDictionary(entry => entry.Tool, entry => entry.Outcome.Text, StringComparer.Ordinal),
            [.. read.Where(entry => entry.Outcome.Text.Length == 0)
                    .Select(entry => $"{entry.Tool}: {entry.Outcome.Reason}")]);
    }

    /// <summary>The text this server should advertise for a tool. NEVER empty: a missing, blank or
    /// unreadable file yields the caller's built-in literal, because a tool with no description is a
    /// tool no agent can route to, and a blank file is a far likelier accident than a deliberate
    /// silence. Files override the literal; they never replace it as the floor.</summary>
    public string DescriptionFor(string toolName, string builtIn) =>
        byToolName.TryGetValue(toolName, out var text) ? text : builtIn;

    /// <summary>An unreadable file is a fallback with a reason, not a throw: one locked file must not
    /// stop a server whose every tool has a perfectly good compiled description.</summary>
    private static (string Text, string Reason) ReadText(string file)
    {
        try
        {
            var text = File.ReadAllText(file).Trim();
            return (text, text.Length == 0 ? "the file is blank" : string.Empty);
        }
        catch (Exception fault) when (fault is IOException or UnauthorizedAccessException)
        {
            return (string.Empty, fault.Message);
        }
    }

    private static void GuardAgainstAnUnknownSet(string directory, string set, string root)
    {
        if (Directory.Exists(root))
        {
            return;
        }

        var what = set.Length > 0 ? $"Tool description set '{set}'" : "Tool description directory";
        throw new InvalidOperationException($"{what} was not found at '{root}'. {SetsPresentIn(directory)}");
    }

    private static string SetsPresentIn(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return $"Directory '{directory}' does not exist.";
        }

        var sets = Directory
            .EnumerateDirectories(directory)
            .Select(path => new DirectoryInfo(path).Name)
            .Order(StringComparer.Ordinal)
            .ToList();

        return sets.Count > 0 ? $"Sets present: {string.Join(", ", sets)}." : "That directory holds no sets.";
    }
}
