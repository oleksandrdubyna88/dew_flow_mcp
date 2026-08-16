namespace Workspace.Application;

/// <summary>Reads a file from inside the workspace sandbox. The port; the adapter enforces the root.</summary>
public interface ISandboxedFileReader
{
    /// <summary>The sandbox these reads are confined to. Part of the port because "which workspace"
    /// is half of what a read means — a call recorded without it says a file was read and not which.</summary>
    string Sandbox { get; }

    Task<FileReadOutcome> ReadAsync(FileReadRequest request, CancellationToken cancellationToken);
}

/// <summary>What to read. <paramref name="StartLine"/> is 1-based inclusive; 0 means "from the top",
/// and a <paramref name="LineCount"/> of 0 means "to the end".
/// <para>Both are client numbers, so both are range-checked by the adapter before any arithmetic: a
/// NEGATIVE is refused naming the legal range, while a count larger than the file is clamped to it —
/// "everything from line 2" is a legitimate request, not a nonsense one.</para></summary>
public sealed record FileReadRequest(string Path, int StartLine = 0, int LineCount = 0);

/// <summary>The read's result. A closed union: a refusal carries its reason instead of arriving as an
/// empty file, which is the failure mode that makes a sandbox breach look like a missing file.</summary>
public abstract record FileReadOutcome
{
    private FileReadOutcome() { }

    /// <summary><paramref name="TotalLines"/> is always the file's REAL total, whatever the window
    /// contains — it is the number the caller pages by, so a capped read that shrank it would send the
    /// caller looking for the end of a file it had already been told it reached.</summary>
    public sealed record Ok(
        string Content,
        int StartLine,
        int EndLine,
        int TotalLines,
        ReadTruncation Truncation) : FileReadOutcome;

    public sealed record Refused(string Reason) : FileReadOutcome;
}

/// <summary>Whether a server cap cut the window short, and which one.
/// <para>The flag-and-reason shape of the family's <c>Captured</c>, for the same reason: a silent
/// truncation is WORSE than an unbounded read. The caller cannot tell a short file from a clipped one,
/// so it reads the end of the window as the end of the file and stops paging — a wrong answer that
/// looks exactly like a right one. Carrying the fact is what makes the cap safe to have.</para></summary>
public sealed record ReadTruncation(bool Clipped, string Reason)
{
    /// <summary>The ordinary case: the caller got the whole window it asked for.</summary>
    public static ReadTruncation None { get; } = new(false, string.Empty);

    public static ReadTruncation By(string reason) => new(true, reason);
}
