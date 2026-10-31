using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Mcp.Tests.Leftovers;

/// <summary>A named pipe on Linux, made by <c>mkfifo(3)</c> through P/Invoke (no child process): the entry that blocks a plain
/// <c>open</c> for reading until a writer comes. Only the tests use it.</summary>
[SupportedOSPlatform("linux")]
internal static partial class PosixFifo
{
    private const int OWriteOnly = 1, ONonBlock = 0x800;

    /// <summary>Make a FIFO at <paramref name="path"/> (mode 0600); the path must not exist.</summary>
    internal static void Make(string path)
    {
        if (Mkfifo(path, 0x180) != 0) throw new IOException($"mkfifo {path} failed ({Marshal.GetLastPInvokeError()})");
    }

    /// <summary>Let a reader that is blocked in <c>open</c> go: a writer's non-blocking open of the same FIFO completes it. Best effort — the
    /// FIFO may be gone, or nobody may be waiting (ENXIO).</summary>
    internal static void ReleaseReaders(string path)
    {
        var fd = Open(path, OWriteOnly | ONonBlock);
        if (fd >= 0) _ = Close(fd);
    }

    [LibraryImport("libc", EntryPoint = "mkfifo", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    private static partial int Mkfifo(string path, uint mode);

    [LibraryImport("libc", EntryPoint = "open", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    private static partial int Open(string path, int flags);

    [LibraryImport("libc", EntryPoint = "close")]
    private static partial int Close(int fd);
}
