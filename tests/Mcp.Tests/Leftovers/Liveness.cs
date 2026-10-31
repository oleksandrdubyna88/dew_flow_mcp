using System.Buffers.Binary;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Mcp.Tests.Leftovers;

/// <summary>What a probe of one pid found. Three NAMED answers, never a flag with an invisible third value:
/// <c>Dead</c> is "no such process" and the ONE answer that can mean gone; <c>Unknown</c> is "the query was
/// refused or failed, or the pid is not a process id at all"; <c>Live</c> carries <c>Start</c> on Linux only.</summary>
public enum ProbeState { Live, Dead, Unknown }

public sealed record Probe(ProbeState State, long? Start);

/// <summary>The reads a liveness verdict is built from (rule §3): the process probe, the start of a process, the
/// clock-tick rate and the pid namespace. Nothing here spawns a process — a `/proc` read, a native process API —
/// and nothing here uses <c>Process.GetProcessById</c>, whose Linux answer under <c>hidepid</c> and whose Windows
/// access rules are .NET's own and not the contract's.</summary>
public static partial class Liveness
{
    private const long MaxSafeInteger = (1L << 53) - 1;
    private const int AtClktck = 17;

    private static readonly Lazy<long?> Ticks = new(ReadClockTicks);

    /// <summary>Is <paramref name="pid"/> alive on this machine? Bounds first, never an unchecked cast: a pid below 1
    /// addresses a process GROUP, a pid past what the OS call takes addresses nothing.</summary>
    public static Probe ProbeProcess(long pid)
    {
        var unknown = new Probe(ProbeState.Unknown, null);
        if (pid < 1 || pid > MaxSafeInteger) return unknown;
        if (OperatingSystem.IsLinux()) return pid > int.MaxValue ? unknown : ProbeLinux((int)pid);
        if (OperatingSystem.IsWindows())
        {
            // OpenProcess takes a DWORD, and Windows has no pid 1: a bash started by a native process reports it for itself.
            return pid > uint.MaxValue || pid == 1 ? unknown : new Probe(Native.WindowsProcessState((uint)pid), null);
        }

        return unknown; // macOS: not built
    }

    /// <summary>On Linux, is process 1 invisible? Then a missing process proves nothing (`hidepid`).</summary>
    public static bool HidepidActive() => OperatingSystem.IsLinux() && !Directory.Exists("/proc/1");

    /// <summary>The kernel's clock-tick rate, read ONCE from the auxiliary vector (<c>AT_CLKTCK</c>); null where it
    /// cannot be read — never a guessed 100.</summary>
    public static long? ClockTicksPerSecond() => Ticks.Value;

    /// <summary>The pure form: <c>AT_CLKTCK</c> from pairs of machine words, found before the <c>AT_NULL</c> that ends
    /// the vector. A vector that never reaches it, an odd length, an unusual word width or a rate of zero gives null.</summary>
    public static long? ClockTicksFromAuxv(ReadOnlySpan<byte> auxv, int wordBytes, bool littleEndian)
    {
        if (wordBytes is not (4 or 8) || auxv.Length == 0 || auxv.Length % (2 * wordBytes) != 0) return null;
        long? rate = null;
        for (var at = 0; at < auxv.Length; at += 2 * wordBytes)
        {
            var type = ReadWord(auxv[at..], wordBytes, littleEndian);
            if (type == 0) return rate;
            if (type != AtClktck || rate is not null) continue;
            var value = ReadWord(auxv[(at + wordBytes)..], wordBytes, littleEndian);
            if (value == 0 || value > MaxSafeInteger) return null;
            rate = (long)value;
        }

        return null;
    }

    /// <summary><c>floor(btime + field22 / ticks)</c> of a <c>/proc/&lt;pid&gt;/stat</c> text: integer Unix seconds. The
    /// command name (field 2) may hold spaces and parentheses, so the fields are counted after the LAST closing one.</summary>
    public static long? StartFromProcStat(string statText, long btime, long? ticks)
    {
        if (ticks is not > 0) return null;
        var close = statText.LastIndexOf(')');
        if (close < 0) return null;
        // Field 3 is index 0 here, so field 22 is index 19.
        var fields = statText[(close + 1)..].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (fields.Length < 20 || !ulong.TryParse(fields[19], NumberStyles.None, CultureInfo.InvariantCulture, out var field)) return null;
        return (long)Math.Floor(btime + (double)field / ticks.Value);
    }

    /// <summary>This process's pid namespace: the decimal inode of <c>/proc/self/ns/pid</c>; null off Linux.</summary>
    public static long? ReadPidNamespace()
    {
        if (!OperatingSystem.IsLinux()) return null;
        try
        {
            var link = new FileInfo("/proc/self/ns/pid").LinkTarget;
            var match = link is null ? null : PidNamespace().Match(link);
            return match is { Success: true } && long.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var inode) ? inode : null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    /// <summary>The start of a LIVE process on Linux from its <c>stat</c> and the boot time, or null when unreadable.</summary>
    internal static long? ReadLinuxStart(long pid)
    {
        try
        {
            var btime = BootTime().Match(File.ReadAllText("/proc/stat"));
            return btime.Success && long.TryParse(btime.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var boot)
                ? StartFromProcStat(File.ReadAllText($"/proc/{pid}/stat"), boot, ClockTicksPerSecond())
                : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static Probe ProbeLinux(int pid) => Native.Kill(pid) switch
    {
        0 => new Probe(ProbeState.Live, ReadLinuxStart(pid)),
        Native.Esrch => new Probe(ProbeState.Dead, null),
        _ => new Probe(ProbeState.Unknown, null), // EPERM or any other errno: the process may be there, nothing is known
    };

    private static long? ReadClockTicks()
    {
        if (!OperatingSystem.IsLinux()) return null;
        try
        {
            return ClockTicksFromAuxv(File.ReadAllBytes("/proc/self/auxv"), IntPtr.Size, BitConverter.IsLittleEndian);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static ulong ReadWord(ReadOnlySpan<byte> bytes, int wordBytes, bool littleEndian) => (wordBytes, littleEndian) switch
    {
        (8, true) => BinaryPrimitives.ReadUInt64LittleEndian(bytes),
        (8, false) => BinaryPrimitives.ReadUInt64BigEndian(bytes),
        (_, true) => BinaryPrimitives.ReadUInt32LittleEndian(bytes),
        _ => BinaryPrimitives.ReadUInt32BigEndian(bytes),
    };

    [GeneratedRegex(@"^pid:\[([0-9]+)\]\z")]
    private static partial Regex PidNamespace();

    [GeneratedRegex(@"^btime[ \t]+([0-9]+)[ \t]*$", RegexOptions.Multiline)]
    private static partial Regex BootTime();
}
