using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using Xunit;

namespace Mcp.Tests.Leftovers;

/// <summary>Scenario 0 of the leftovers plan: what the machine running the tests IS, measured and printed
/// before any helper code is trusted. It asserts nothing about the helper and never fails — it RECORDS.
///
/// <para>The question it answers first: who owns a file a test makes? On a hosted Windows runner the job runs
/// elevated, and an elevated process's new files are likely owned by <c>BUILTIN\Administrators</c>, not by the
/// user's own SID. The kernel-owner key of the sweep (rule §3, "scope evidence") compares that owner with the
/// caller, so every Windows assertion later in this folder is written against the answer printed here, never
/// against a guess. Windows reads go through the managed ACL API: the owner of a plain file is the same
/// security-descriptor field the native reads later take.</para>
///
/// <para>The lines go out as test warnings (<see cref="TestContext.AddWarning"/>), which the runner prints
/// under the test even when it passes.</para></summary>
public sealed class RunnerFactsTests
{
    [Fact]
    public void The_runner_facts_are_recorded()
    {
        Record($"runner facts: os={System.Runtime.InteropServices.RuntimeInformation.OSDescription}, arch={System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture}, pid={Environment.ProcessId}");
        if (OperatingSystem.IsWindows()) WindowsFacts();
        else if (OperatingSystem.IsLinux()) LinuxFacts();
        else Record("runner facts: this platform is not one the leftovers helper supports (macOS is not built)");
    }

    private static void Record(string line) => TestContext.Current.AddWarning(line);

    [SupportedOSPlatform("windows")]
    private static void WindowsFacts()
    {
        var user = WindowsIdentity.GetCurrent();
        var elevated = new WindowsPrincipal(user).IsInRole(WindowsBuiltInRole.Administrator);

        // testsToDelete-exempt: raw-temp — the measuring test must see the raw temp root once
        var temp = Path.GetTempPath();
        var file = Path.Combine(temp, $"mcp-runner-facts-{Guid.NewGuid():N}.txt");
        try
        {
            File.WriteAllText(file, "runner facts");
            var owner = new FileInfo(file).GetAccessControl().GetOwner(typeof(SecurityIdentifier));
            Record($"runner facts: new file owner SID = {owner?.Value ?? "(unreadable)"}");
            Record($"runner facts: token user SID = {user.User?.Value}; token owner SID = {user.Owner?.Value}");
            Record($"runner facts: file owner equals token user SID = {owner is not null && owner.Equals(user.User)}");
        }
        finally
        {
            File.Delete(file);
        }

        Record($"runner facts: token is elevated (Administrators) = {elevated}");
        var profile = FinalPath(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        var finalTemp = FinalPath(temp);
        Record($"runner facts: temp = {finalTemp}; profile = {profile}; temp is inside the profile = {IsInside(finalTemp, profile)}");
    }

    private static string FinalPath(string path)
    {
        var info = new DirectoryInfo(path);
        return (info.ResolveLinkTarget(returnFinalTarget: true) ?? info).FullName.TrimEnd(Path.DirectorySeparatorChar);
    }

    private static bool IsInside(string child, string parent) =>
        child.Length > parent.Length
        && child.StartsWith(parent, StringComparison.OrdinalIgnoreCase)
        && child[parent.Length] == Path.DirectorySeparatorChar;

    private static void LinuxFacts()
    {
        var uid = File.ReadLines("/proc/self/status").First(l => l.StartsWith("Uid:", StringComparison.Ordinal));
        Record($"runner facts: {uid.Replace('\t', ' ')} (real, effective, saved, fs)");
        Record($"runner facts: /proc/1 visible = {Directory.Exists("/proc/1")} (false would mean hidepid)");
        Record($"runner facts: AT_CLKTCK from /proc/self/auxv = {ClockTicks()?.ToString() ?? "(unreadable)"}");
    }

    /// <summary>AT_CLKTCK (17) from the auxiliary vector: pairs of machine words, ended by AT_NULL.</summary>
    private static long? ClockTicks()
    {
        var auxv = File.ReadAllBytes("/proc/self/auxv");
        var word = IntPtr.Size;
        for (var at = 0; at + 2 * word <= auxv.Length; at += 2 * word)
        {
            var type = Read(auxv, at, word);
            if (type == 0) return null;
            if (type == 17) return Read(auxv, at + word, word);
        }

        return null;
    }

    private static long Read(byte[] bytes, int at, int word) =>
        word == 8 ? BitConverter.ToInt64(bytes, at) : BitConverter.ToUInt32(bytes, at);
}
