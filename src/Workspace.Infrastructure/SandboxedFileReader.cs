using System.Text;
using Mcp.Contracts;
using Microsoft.Extensions.Logging;
using Workspace.Application;

namespace Workspace.Infrastructure;

/// <summary>What ONE read may materialize and hand back.
/// <para>Two caps, because either alone bounds nothing. A LINE cap does not stop a minified bundle,
/// a one-line JSON document or a base64 blob — all of those are one line, and one line is however
/// many megabytes it is. A BYTE cap alone would let five thousand short lines through as a single
/// content block nobody asked for. The pair is the bound.</para>
/// <para>Configuration rather than constants, and <c>TryAdd</c>-registered, so a host whose workspace
/// holds legitimately huge files raises them deliberately — while a host that says nothing gets a
/// ceiling rather than none.</para></summary>
public sealed record SandboxedFileReaderOptions
{
    /// <summary>The most lines one read returns. Five thousand: far past any source file worth reading
    /// whole (this repository's largest is under three hundred), and short of the runaway.</summary>
    public int MaxLines { get; init; } = 5_000;

    /// <summary>The most UTF-8 bytes one read returns. One mebibyte — already a very large single
    /// content block for a protocol whose consumer is a model with a context window, and the only cap
    /// that binds when the file is one enormous line.</summary>
    public int MaxBytes { get; init; } = 1024 * 1024;
}

/// <summary>Reads files under one workspace root and nothing else.
/// <para>The guard resolves the full path and compares it against the resolved root, which is what
/// stops <c>..</c> traversal and symlink escapes alike — checking the STRING for "<c>..</c>" does not,
/// because <c>a/../../b</c> and a link both spell fine.</para>
/// <para>The read STREAMS. It used to be <c>File.ReadAllLinesAsync</c> — the entire file into a
/// <c>string[]</c> whatever its size — with the window sliced afterwards, so a single large file in
/// the workspace was a multi-hundred-megabyte allocation on a process that never restarts. Memory now
/// scales with the WINDOW; the enumeration still runs to the end, because the caller pages by the real
/// total and that is the only place it can be counted. The pass costs I/O and no memory, it is no
/// more I/O than the old read already did, and <c>ToolCatalogOptions.CallTimeout</c> is what bounds
/// how long it may take.</para></summary>
public sealed class SandboxedFileReader : ISandboxedFileReader
{
    private readonly WorkspaceRoot root;
    private readonly SandboxedFileReaderOptions caps;
    private readonly ILogger<SandboxedFileReader> logger;

    public SandboxedFileReader(
        WorkspaceRoot root,
        SandboxedFileReaderOptions caps,
        ILogger<SandboxedFileReader> logger)
    {
        GuardAgainstUnusableCaps(caps);
        this.root = root;
        this.caps = caps;
        this.logger = logger;
    }

    public string Sandbox => root.FullPath;

    public async Task<FileReadOutcome> ReadAsync(FileReadRequest request, CancellationToken cancellationToken)
    {
        if (!TryResolveInsideRoot(request.Path, out var fullPath))
        {
            logger.LogWarning("Refused a read outside the workspace root: {RequestedPath}", request.Path);
            return new FileReadOutcome.Refused($"Path '{request.Path}' is outside the workspace.");
        }

        // The client's numbers are checked BEFORE any arithmetic touches them. They arrive straight
        // from a JSON body, and the window below used to add them in int: `startLine + lineCount`
        // wrapped negative and threw out of an unguarded range indexer, which any caller could reach
        // in one call.
        var illegal = OutOfRange(request);
        if (illegal.Length > 0)
        {
            logger.LogWarning("Refused a read with an out-of-range window for {RequestedPath}: {Reason}", request.Path, illegal);
            return new FileReadOutcome.Refused(illegal);
        }

        if (!File.Exists(fullPath))
        {
            return new FileReadOutcome.Refused($"File '{request.Path}' does not exist.");
        }

        return await WindowAsync(fullPath, request, cancellationToken);
    }

    /// <summary>The reason this window is nonsense, or empty when it is legal.
    /// <para>Only NEGATIVES are refused. A huge count is not nonsense — "everything from line 2" is
    /// what a pager sends — so it is clamped to the file below rather than declined.</para></summary>
    private static string OutOfRange(FileReadRequest request) =>
        (request.StartLine, request.LineCount) switch
        {
            ( < 0, _) => $"'startLine' must be a whole number between 0 and {int.MaxValue}; 0 starts at the top.",
            (_, < 0) => $"'lineCount' must be a whole number between 0 and {int.MaxValue}; 0 reads to the end.",
            _ => string.Empty,
        };

    /// <summary>Streams the file once, keeping the window and counting the rest.
    /// <para>A start past the end is not an error — it falls out of this as empty content with the real
    /// total, so the caller corrects the offset by number instead of guessing.</para></summary>
    private async Task<FileReadOutcome> WindowAsync(
        string fullPath,
        FileReadRequest request,
        CancellationToken cancellationToken)
    {
        // int.MaxValue, not long.MaxValue, for "to the end": `start + requested - 1` below must stay
        // inside a long, and both operands being int-ranged is what guarantees that. The window is
        // still computed in long — see the overflow this arithmetic used to carry.
        long requested = request.LineCount == 0 ? int.MaxValue : request.LineCount;
        var window = new Window(Math.Max(1, request.StartLine), requested, caps);

        var total = 0;
        await foreach (var line in File.ReadLinesAsync(fullPath, cancellationToken))
        {
            total++;
            window.Offer(total, line);
        }

        var outcome = window.Close(total);
        LogIfCapped(request, outcome);
        return outcome;
    }

    /// <summary>A capped read is the operator's business as well as the caller's: repeated cuts are how
    /// you learn a workspace holds something nobody should be reading whole, or that the cap is set
    /// wrong for this host. Rare by definition, so it goes in at Information rather than Debug.</summary>
    private void LogIfCapped(FileReadRequest request, FileReadOutcome outcome)
    {
        if (outcome is not FileReadOutcome.Ok { Truncation.Clipped: true } capped)
        {
            return;
        }

        logger.LogInformation(
            "Capped a read of {RequestedPath} at lines {StartLine}-{EndLine} of {TotalLines}: {Reason}",
            request.Path, capped.StartLine, capped.EndLine, capped.TotalLines, capped.Truncation.Reason);
    }

    /// <summary>A cap that cannot bound anything stops the host at composition, where one message is
    /// read once — rather than producing an empty answer on every read for the life of a process nobody
    /// is watching.</summary>
    private static void GuardAgainstUnusableCaps(SandboxedFileReaderOptions caps)
    {
        if (caps.MaxLines <= 0 || caps.MaxBytes <= 0)
        {
            throw new InvalidOperationException(
                $"{nameof(SandboxedFileReaderOptions)}.{nameof(SandboxedFileReaderOptions.MaxLines)} and "
                + $".{nameof(SandboxedFileReaderOptions.MaxBytes)} must both be positive; got "
                + $"{caps.MaxLines} and {caps.MaxBytes}.");
        }
    }

    /// <summary>The window under construction while the file streams past it. It holds the WINDOW and
    /// never the file — which is the whole of the rewrite; everything else here is bookkeeping so the
    /// answer can say what it left out.</summary>
    private sealed class Window(int start, long requested, SandboxedFileReaderOptions caps)
    {
        private readonly List<string> lines = [];
        private readonly long wanted = Math.Min(requested, caps.MaxLines);
        private long bytes;
        private bool byteStopped;

        /// <summary>Offers the line at <paramref name="number"/>, in file order. Lines before the
        /// window, after it, and past a cap are dropped here; the caller keeps counting regardless,
        /// because the total is what the caller pages by.</summary>
        public void Offer(int number, string line)
        {
            if (number < start || lines.Count >= wanted || byteStopped)
            {
                return;
            }

            Take(line);
        }

        public FileReadOutcome Close(int total)
        {
            // Apply is a no-op unless ONE line was itself over budget: the loop below never lets the
            // window cross the cap otherwise. It is the shared clipper rather than a second one here
            // because cutting UTF-8 at a byte budget without splitting a surrogate pair is exactly the
            // problem PayloadBudget already solves.
            var (content, dropped) = PayloadBudget.Apply(string.Join('\n', lines), caps.MaxBytes);
            var end = start + lines.Count - 1;
            return new FileReadOutcome.Ok(content, start, end, total, Clipped(total, end, dropped));
        }

        /// <summary>Appends one line unless it would push the window past the byte cap. The FIRST line
        /// is taken whatever its size and clipped at close instead: a window that comes back empty
        /// cannot be paged past, so an over-long single line has to yield SOMETHING plus the fact that
        /// it was cut.</summary>
        private void Take(string line)
        {
            var cost = Encoding.UTF8.GetByteCount(line) + 1; // + the '\n' that will join it to the next
            if (lines.Count > 0 && bytes + cost > caps.MaxBytes)
            {
                byteStopped = true;
                return;
            }

            lines.Add(line);
            bytes += cost;
        }

        /// <summary>Which cap, if any, cut this window short — and what the caller does next, which is
        /// decided HERE because only here is it known whether paging can help at all. Ordered by what
        /// the caller can act on: the line it cannot page around first, then the byte cap, then lines.</summary>
        private ReadTruncation Clipped(int total, int end, int dropped) =>
            (dropped, byteStopped, Short: end < Reachable(total)) switch
            {
                ( > 0, _, _) => ReadTruncation.By(
                    $"line {start} alone is longer than this server's {caps.MaxBytes}-byte read cap, and "
                    + $"{dropped} further byte(s) of that one line were dropped. Paging cannot reach them — "
                    + "narrow the request another way."),
                (_, true, _) => ReadTruncation.By(
                    $"the window stops at this server's {caps.MaxBytes}-byte read cap. "
                    + $"Ask again with startLine {end + 1} to continue."),
                (_, _, true) => ReadTruncation.By(
                    $"the window stops at this server's {caps.MaxLines}-line read cap. "
                    + $"Ask again with startLine {end + 1} to continue."),
                _ => ReadTruncation.None,
            };

        /// <summary>The line an UNCAPPED read of this request would have ended at.</summary>
        private long Reachable(int total) => Math.Min(total, start + requested - 1);
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
