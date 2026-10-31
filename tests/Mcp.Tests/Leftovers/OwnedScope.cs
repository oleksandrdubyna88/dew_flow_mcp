namespace Mcp.Tests.Leftovers;

/// <summary>Many folders, one <c>Dispose</c> — a test class's field. xUnit v3 creates one instance per test and disposes it
/// after the test whether it passed or threw: that is this language's <c>t.after</c>. Disposal is last-made-first, so a
/// folder made under another goes before it, and never throws.</summary>
public sealed class OwnedScope(Action<string>? warn = null) : IDisposable
{
    private readonly List<OwnedDir> _dirs = [];
    private readonly Lock _gate = new();

    /// <summary>A folder named for <paramref name="what"/>, claimed first, in <paramref name="parent"/> (default: the temp
    /// root), removed when the scope is disposed.</summary>
    public string Dir(string what, string? parent = null)
    {
        var dir = OwnedDir.Create(what, parent, warn);
        lock (_gate) _dirs.Add(dir);
        return dir.Path;
    }

    public void Dispose()
    {
        OwnedDir[] dirs;
        lock (_gate)
        {
            dirs = [.. _dirs];
            _dirs.Clear();
        }

        for (var i = dirs.Length - 1; i >= 0; i--)
        {
            try
            {
                dirs[i].Dispose();
            }
            catch (Exception ex)
            {
                (warn ?? Owned.DefaultWarn)($"owned: disposing {dirs[i].Path} failed ({ex.GetType().Name}: {ex.Message}) — left for the start-of-run sweep");
            }
        }
    }
}
