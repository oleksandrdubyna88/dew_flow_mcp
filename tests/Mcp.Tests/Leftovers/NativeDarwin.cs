namespace Mcp.Tests.Leftovers;

/// <summary>What <c>lstat</c> answered on macOS, read at the fixed offsets of Darwin's 64-bit-inode <c>struct stat</c>. Kept whole (not only the
/// <see cref="Identity"/>) so the layout self-test can check every field this reads against something known.</summary>
internal readonly record struct DarwinStat(
    int Dev, ushort Mode, ushort Nlink, ulong Ino, uint Uid, uint Gid, int Rdev,
    DateTimeOffset Atime, DateTimeOffset Mtime, DateTimeOffset Ctime, DateTimeOffset Birthtime, long Size);

internal static partial class Native
{
    internal const int DarwinStatSize = 144;

    internal static DarwinStat ParseDarwinStat(ReadOnlySpan<byte> buffer) =>
        throw new PlatformNotSupportedException($"the Darwin native path is not built yet ({buffer.Length} bytes were given)");

    internal static DarwinStat? DarwinLstat(string path) =>
        throw new PlatformNotSupportedException($"the Darwin native path is not built yet ({path})");
}
