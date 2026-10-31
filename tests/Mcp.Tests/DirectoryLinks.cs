using Xunit;

namespace Mcp.Tests;

internal static class DirectoryLinks
{
    /// <summary>A DIRECTORY link falls back to an NTFS junction where symlinks need a privilege this
    /// run does not hold: a junction needs none, the reader follows it exactly as transparently, and a
    /// cloned repository can carry one — so the escape the guard exists for stays testable on a stock
    /// Windows box instead of skipping there forever.</summary>
    public static void CreateOrSkip(string link, string target)
    {
        try
        {
            Directory.CreateSymbolicLink(link, target);
            return;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            if (!OperatingSystem.IsWindows())
            {
                Assert.Skip($"cannot create directory links here ({ex.Message})");
            }
        }

        using var mklink = System.Diagnostics.Process.Start(
            new System.Diagnostics.ProcessStartInfo("cmd.exe", $"/c mklink /J \"{link}\" \"{target}\"")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            });
        mklink!.WaitForExit();
        if (mklink.ExitCode != 0 || !Directory.Exists(link))
        {
            Assert.Skip("cannot create a directory symlink or a junction here");
        }
    }
}
