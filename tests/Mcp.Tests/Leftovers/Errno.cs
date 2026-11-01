namespace Mcp.Tests.Leftovers;

/// <summary>The errno values the helper compares, one table per system — never the Linux numbers on Darwin. They agree for the classic ones
/// (ENOENT 2, ESRCH 3, EPERM 1, EEXIST 17, ENOTDIR 20, EISDIR 21, EINVAL 22) and differ for the rest: ENOTEMPTY is 39 on Linux and 66 on
/// Darwin, ENOTSUP 95 and 45, ENOSYS 38 and 78. Values from <c>asm-generic/errno*.h</c> and Darwin's <c>sys/errno.h</c>.</summary>
internal readonly record struct ErrnoTable(int Enoent, int Esrch, int Eperm, int Eexist, int Enotdir, int Eisdir, int Einval, int Enotempty, int Enotsup, int Enosys)
{
    /// <summary>The GNU/Linux table (the same numbers on x86, ARM and RISC-V; MIPS and SPARC differ and are not measured).</summary>
    internal static readonly ErrnoTable Linux = new(2, 3, 1, 17, 20, 21, 22, 39, 95, 38);

    /// <summary>The Darwin table (macOS, 64-bit).</summary>
    internal static readonly ErrnoTable Darwin = new(2, 3, 1, 17, 20, 21, 22, 66, 45, 78);
}

/// <summary>The tables by name (a code that serves ONE system says which), and the table of the system this process runs on.</summary>
internal static class Errno
{
    internal static ErrnoTable Linux => ErrnoTable.Linux;

    internal static ErrnoTable Darwin => ErrnoTable.Darwin;

    internal static ErrnoTable Current => OperatingSystem.IsMacOS() ? ErrnoTable.Darwin : ErrnoTable.Linux;
}
