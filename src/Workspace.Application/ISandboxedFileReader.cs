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
/// and a <paramref name="LineCount"/> of 0 means "to the end".</summary>
public sealed record FileReadRequest(string Path, int StartLine = 0, int LineCount = 0);

/// <summary>The read's result. A closed union: a refusal carries its reason instead of arriving as an
/// empty file, which is the failure mode that makes a sandbox breach look like a missing file.</summary>
public abstract record FileReadOutcome
{
    private FileReadOutcome() { }

    public sealed record Ok(string Content, int StartLine, int EndLine, int TotalLines) : FileReadOutcome;

    public sealed record Refused(string Reason) : FileReadOutcome;
}
