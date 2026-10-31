using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using FluentAssertions;
using Xunit;

namespace Mcp.Tests.Leftovers;

/// <summary>The kernel owner is the scope key (rule §3, "scope evidence"): the name selects, the file system
/// authorises. POSIX: the entry's own uid is the caller's real uid. Windows (owner decision 2): the entry is
/// inside the caller's profile AND its owner SID is the caller's user SID — both read from one handle that opens
/// the entry itself. Any failure is "not mine".</summary>
public sealed class ScopeTests
{
    [Fact]
    public void A_file_this_test_made_is_mine_exactly_where_the_runner_makes_files_the_callers()
    {
        using var scratch = new ScratchDir();
        var file = scratch.File("mine.txt");
        File.WriteAllText(file, "x");

        // POSIX: always. Windows: the answer scenario 0 printed — true where the new file's owner SID is the
        // user's and it lies inside the profile, false where it is Administrators (an elevated runner). Asserted,
        // never skipped.
        Scope.IsMine(file).Should().Be(!OperatingSystem.IsWindows() || WindowsMeasuredIsMine(file));
    }

    [Fact]
    public void A_missing_path_is_not_mine()
    {
        using var scratch = new ScratchDir();

        Scope.IsMine(scratch.File("nope")).Should().BeFalse("an entry that cannot be read is no evidence");
    }

    [Fact]
    public void On_windows_both_outcomes_are_made_on_any_runner_through_the_fixtures()
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("the owner SID and profile keys — Windows only");

        if (OperatingSystem.IsWindows()) WindowsBothOutcomes();
    }

    [Fact]
    public void A_link_is_judged_on_its_own_owner_never_on_its_targets()
    {
        using var scratch = new ScratchDir();
        var link = scratch.File("link");
        IDisposable? cleanup = null;
        string target;
        if (OperatingSystem.IsWindows())
        {
            (target, cleanup) = WindowsForeignTarget(scratch);
        }
        else
        {
            target = "/etc/passwd";
            if (Native.IdentityOf(target)?.Uid == Native.GetUid()) Assert.Skip("the target is the caller's own, so a target's owner cannot differ from the link's");
        }

        using var _ = cleanup;
        CreateFileLinkOrSkip(link, target);
        var plain = scratch.File("plain.txt");
        File.WriteAllText(plain, "x");

        Native.IdentityOf(link)!.Value.IsLink.Should().BeTrue();
        Scope.IsMine(target).Should().BeFalse("the target is not the caller's");
        Scope.IsMine(link).Should().Be(Scope.IsMine(plain), "the link's own owner decides, a link is never followed");
    }

    [Fact]
    public void A_claim_that_is_not_there_is_missing_and_one_there_reads_as_text()
    {
        using var scratch = new ScratchDir();
        File.WriteAllText(scratch.File("claim"), "u1:h@linux:1\n");

        Scope.ReadClaimText(scratch.File("nope")).Should().BeOfType<ClaimRead.Missing>();
        Scope.ReadClaimText(scratch.File("claim"), _ => true).Should().BeOfType<ClaimRead.Text>().Which.Value.Should().Be("u1:h@linux:1\n");
    }

    [Fact]
    public void A_claim_the_scope_key_refuses_is_unreadable_as_a_foreign_uid()
    {
        using var scratch = new ScratchDir();
        File.WriteAllText(scratch.File("claim"), "u1:h@linux:1\n");

        Scope.ReadClaimText(scratch.File("claim"), _ => false).Should().BeOfType<ClaimRead.Unreadable>().Which.Reason.Should().Be("foreign-uid");
    }

    [Fact]
    public void The_default_scope_key_is_the_kernel_owner()
    {
        using var scratch = new ScratchDir();
        var file = scratch.File("claim");
        File.WriteAllText(file, "u1:h@linux:1\n");

        var read = Scope.ReadClaimText(file);

        if (Scope.IsMine(file)) read.Should().BeOfType<ClaimRead.Text>();
        else read.Should().BeOfType<ClaimRead.Unreadable>().Which.Reason.Should().Be("foreign-uid");
    }

    [Fact]
    public void A_folder_named_like_a_claim_is_unreadable_because_only_a_regular_file_is_read()
    {
        using var scratch = new ScratchDir();
        Directory.CreateDirectory(scratch.File("claim"));

        Scope.ReadClaimText(scratch.File("claim"), _ => true).Should().BeOfType<ClaimRead.Unreadable>().Which.Reason.Should().BeNull();
    }

    [Fact]
    public void A_link_named_like_a_claim_is_unreadable_because_a_link_is_never_followed()
    {
        using var scratch = new ScratchDir();
        var target = scratch.File("real");
        File.WriteAllText(target, "u1:h@linux:1\n");
        CreateFileLinkOrSkip(scratch.File("claim"), target);

        Scope.ReadClaimText(scratch.File("claim"), _ => true).Should().BeOfType<ClaimRead.Unreadable>();
    }

    [Fact]
    public void At_most_one_byte_past_the_limit_is_read_so_a_513_byte_claim_is_seen_as_too_long()
    {
        using var scratch = new ScratchDir();
        File.WriteAllText(scratch.File("long"), new string('x', 2000));

        var text = Scope.ReadClaimText(scratch.File("long"), _ => true).Should().BeOfType<ClaimRead.Text>().Subject.Value;

        text.Should().HaveLength(513, "512 bytes are the most a claim may hold; the 513th makes it unreadable");
    }

    [SupportedOSPlatform("windows")]
    private static bool WindowsMeasuredIsMine(string file)
    {
        // The measured answer from independent reads: the managed ACL API for the owner, full paths for the profile.
        var user = WindowsIdentity.GetCurrent().User;
        var owner = new FileInfo(file).GetAccessControl().GetOwner(typeof(SecurityIdentifier));
        var profile = new DirectoryInfo(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)).FullName.TrimEnd('\\');
        var inside = new FileInfo(file).FullName.StartsWith(profile + "\\", StringComparison.OrdinalIgnoreCase);
        return inside && owner is not null && owner.Equals(user);
    }

    [SupportedOSPlatform("windows")]
    private static (string Target, IDisposable Cleanup) WindowsForeignTarget(ScratchDir scratch)
    {
        var ownership = new WindowsOwnership();
        var made = scratch.File("target.txt");
        File.WriteAllText(made, "x");
        return (ownership.ForeignToCaller(made), ownership);
    }

    [SupportedOSPlatform("windows")]
    private static void WindowsBothOutcomes()
    {
        using var scratch = new ScratchDir();
        using var ownership = new WindowsOwnership();
        var mine = scratch.File("mine.txt");
        File.WriteAllText(mine, "x");
        var foreign = scratch.File("foreign.txt");
        File.WriteAllText(foreign, "x");

        ownership.OwnedByCaller(mine);
        var foreignPath = ownership.ForeignToCaller(foreign);

        Scope.IsMine(mine).Should().BeTrue("the owner SID is the caller's and the file lies in the profile");
        Native.OwnerSidOf(mine).Should().Be(WindowsIdentity.GetCurrent().User!);
        Scope.IsMine(foreignPath).Should().BeFalse($"taken branch: {ownership.LastForeignBranch}");
    }

    private static void CreateFileLinkOrSkip(string link, string target)
    {
        try
        {
            File.CreateSymbolicLink(link, target);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            Assert.Skip($"cannot create symbolic links here ({ex.Message})");
        }
    }
}
