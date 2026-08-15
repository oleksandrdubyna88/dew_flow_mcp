using Microsoft.Extensions.Logging;
using Workspace.Application;

namespace Workspace.Infrastructure;

/// <summary>Reads files under one workspace root and nothing else.
/// <para>The guard resolves the full path and compares it against the resolved root, which is what
/// stops <c>..</c> traversal and symlink escapes alike — checking the STRING for "<c>..</c>" does not,
/// because <c>a/../../b</c> and a link both spell fine.</para></summary>
public sealed class SandboxedFileReader(WorkspaceRoot root, ILogger<SandboxedFileReader> logger) : ISandboxedFileReader
{
    public string Sandbox => root.FullPath;

    public async Task<FileReadOutcome> ReadAsync(FileReadRequest request, CancellationToken cancellationToken)
    {
        if (!TryResolveInsideRoot(request.Path, out var fullPath))
        {
            logger.LogWarning("Refused a read outside the workspace root: {RequestedPath}", request.Path);
            return new FileReadOutcome.Refused($"Path '{request.Path}' is outside the workspace.");
        }

        if (!File.Exists(fullPath))
        {
            return new FileReadOutcome.Refused($"File '{request.Path}' does not exist.");
        }

        var lines = await File.ReadAllLinesAsync(fullPath, cancellationToken);
        return Window(lines, request);
    }

    /// <summary>Slices the requested window. A start past the end is not an error — it returns empty
    /// content with the real total, so the caller can correct the offset instead of guessing.</summary>
    private static FileReadOutcome Window(string[] lines, FileReadRequest request)
    {
        var start = Math.Max(1, request.StartLine == 0 ? 1 : request.StartLine);
        if (start > lines.Length)
        {
            return new FileReadOutcome.Ok(string.Empty, start, start - 1, lines.Length);
        }

        var count = request.LineCount <= 0 ? lines.Length - start + 1 : request.LineCount;
        var end = Math.Min(lines.Length, start + count - 1);
        var slice = lines[(start - 1)..end];
        return new FileReadOutcome.Ok(string.Join('\n', slice), start, end, lines.Length);
    }

    private bool TryResolveInsideRoot(string relativePath, out string fullPath)
    {
        fullPath = string.Empty;
        if (Path.IsPathRooted(relativePath))
        {
            return false;
        }

        var candidate = Path.GetFullPath(Path.Combine(root.FullPath, relativePath));
        if (!candidate.StartsWith(root.FullPath + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            return false;
        }

        fullPath = candidate;
        return true;
    }
}

/// <summary>The one directory the workspace tools may touch.</summary>
public sealed record WorkspaceRoot
{
    public WorkspaceRoot(string fullPath) =>
        FullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(fullPath));

    public string FullPath { get; }
}
