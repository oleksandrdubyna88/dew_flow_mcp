using FluentAssertions;
using Xunit;

namespace Mcp.Tests.Leftovers;

/// <summary>The end-of-run audit (S4 scenarios 7 and 7a): it names only what THIS run claimed, it sees an undisposed folder under
/// any parent and a disposal that gave up, and its message carries each group under its own words. Every scenario that lists a
/// temp root passes a PRIVATE one; the kernel-owner seam stands in for the OS so the answers are the same on an elevated runner.</summary>
public sealed class LeftoversAuditTests : IDisposable
{
    private static readonly Func<string, bool> Mine = _ => true;

    private readonly OwnedScope _owned = new();

    public void Dispose() => _owned.Dispose();

    private static string Other => OwnerFixtures.Other;

    // ---- 7 ---------------------------------------------------------------------------------------------------------------

    [Fact]
    public void The_audit_names_only_what_this_run_claimed()
    {
        var tmp = _owned.Dir("audit-tmp");
        var me = Owner.RunRecord();
        var dead = UnmatchablePid.Next();
        var foreignHost = me with { Host = "other@" + (OperatingSystem.IsWindows() ? "windows" : "linux") };
        var ours = Plant(tmp, Owned.NameFor("ours"), me);
        Plant(tmp, Owned.NameFor("dead"), me with { Pid = dead });
        Plant(tmp, Owned.NameFor("otherowner"), me with { Token = Other });
        Plant(tmp, Owned.NameFor("otherhost"), foreignHost);
        Plant(tmp, Owned.NameFor("unclaimed"), claim: null);
        Plant(tmp, Owned.Prefix + OwnerToken.This + "-zzz-notmine-abc123", me);
        var orphan = Path.Combine(tmp, Owned.NameFor("orphan") + ".owner");
        File.WriteAllText(orphan, me.Format() + "\n");
        File.WriteAllText(Path.Combine(tmp, Owned.NameFor("orphan-dead") + ".owner"), (me with { Pid = dead }).Format() + "\n");

        var claimed = LeftoversAudit.ClaimedBy(tmp, me, Mine);

        claimed.Should().BeEquivalentTo([ours, orphan]);
    }

    [Fact]
    public void A_symlinked_folder_is_not_followed()
    {
        var tmp = _owned.Dir("audit-link-tmp");
        var elsewhere = _owned.Dir("audit-link-target");
        var me = Owner.RunRecord();
        var name = Owned.NameFor("linked");
        File.WriteAllText(Path.Combine(tmp, name) + ".owner", me.Format() + "\n");
        if (!OperatingSystem.IsWindows()) Directory.CreateSymbolicLink(Path.Combine(tmp, name), elsewhere);
        else Assert.Skip("a directory link on Windows needs a junction or a privilege; the POSIX case is the proof");

        LeftoversAudit.ClaimedBy(tmp, me, Mine).Should().BeEmpty("a link is not a folder this run made");
    }

    // ---- 7a --------------------------------------------------------------------------------------------------------------

    [Fact]
    public void An_undisposed_folder_under_any_parent_is_seen_by_the_registry_and_not_by_the_temp_root_scan()
    {
        var parent = _owned.Dir("audit-parent");
        var dir = OwnedDir.Create("custom", parent);
        try
        {
            LeftoversAudit.Unreclaimed().Should().Contain((dir.Path, false));
            LeftoversAudit.ClaimedBy(Owned.TempRoot(), Owner.RunRecord()).Should().NotContain(dir.Path, "it is not at the depth-1 of the temp root");
        }
        finally
        {
            dir.Dispose();
        }

        LeftoversAudit.Unreclaimed().Select(u => u.Path).Should().NotContain(dir.Path);
    }

    [Fact]
    public void A_disposal_that_gave_up_stays_registered_until_a_later_disposal_removes_it()
    {
        var parent = _owned.Dir("audit-stuck-parent");
        var dir = OwnedDir.Create("stuck", parent);
        var release = Unremovable.Make(parent, dir.Path);
        try
        {
            dir.Dispose();

            LeftoversAudit.Unreclaimed().Should().Contain((dir.Path, true));
        }
        finally
        {
            release.Dispose();
        }

        dir.Dispose();
        LeftoversAudit.Unreclaimed().Select(u => u.Path).Should().NotContain(dir.Path);
        Directory.Exists(dir.Path).Should().BeFalse();
    }

    // ---- the message -----------------------------------------------------------------------------------------------------

    [Fact]
    public void The_message_carries_each_group_under_its_own_words_and_omits_an_empty_one()
    {
        var message = LeftoversAudit.Verdict([("/t/a", false), ("/t/b", true), ("/t/c", false)], ["/t/d", "/t/a"]);

        message.Should().Be("mcp tests: this run left 4 folder(s) it claimed — never disposed: /t/a, /t/c; disposed but not removed: /t/b; claimed in the temp root: /t/d");
        LeftoversAudit.Verdict([("/t/b", true)], []).Should().Be("mcp tests: this run left 1 folder(s) it claimed — disposed but not removed: /t/b");
        LeftoversAudit.Verdict([], ["/t/d"]).Should().Be("mcp tests: this run left 1 folder(s) it claimed — claimed in the temp root: /t/d");
        LeftoversAudit.Verdict([], []).Should().BeNull();
    }

    [Fact]
    public void A_run_that_left_a_folder_throws_one_exception_and_leaves_the_temp_root_clean()
    {
        var tmp = _owned.Dir("audit-run-tmp");
        var me = Owner.RunRecord();
        var undisposed = OwnedDir.Create("leaked", tmp);
        var orphan = Path.Combine(tmp, Owned.NameFor("orphan") + ".owner");
        File.WriteAllText(orphan, me.Format() + "\n");
        var claimedFolder = Plant(tmp, Owned.NameFor("claimed"), me);

        var run = () => LeftoversAudit.Run(tmp, me, [undisposed], Mine);

        var message = run.Should().Throw<InvalidOperationException>().Which.Message;
        message.Should().StartWith("mcp tests: this run left 3 folder(s) it claimed — never disposed: " + undisposed.Path);
        message.Should().Contain("claimed in the temp root: ").And.Contain(claimedFolder).And.Contain(orphan);
        Directory.GetFileSystemEntries(tmp).Should().BeEmpty("the audit removes what it names");
        LeftoversAudit.Unreclaimed().Select(u => u.Path).Should().NotContain(undisposed.Path);
    }

    [Fact]
    public void A_clean_run_does_not_throw()
    {
        var tmp = _owned.Dir("audit-clean-tmp");

        var run = () => LeftoversAudit.Run(tmp, Owner.RunRecord(), [], Mine);

        run.Should().NotThrow();
    }

    [Fact]
    public async Task The_assembly_fixture_fails_the_run_with_the_audits_message()
    {
        var fails = new LeftoversSweepFixture(() => SweepResult.Empty, _ => { }, () => throw new InvalidOperationException("mcp tests: this run left 1 folder(s) it claimed — never disposed: x"));
        var passes = new LeftoversSweepFixture(() => SweepResult.Empty, _ => { }, () => { });

        var dispose = async () => await fails.DisposeAsync();

        await dispose.Should().ThrowAsync<InvalidOperationException>().WithMessage("mcp tests: this run left*");
        await passes.DisposeAsync();
    }

    private static string Plant(string tmp, string name, Owner? claim)
    {
        var dir = Path.Combine(tmp, name);
        Directory.CreateDirectory(dir);
        if (claim is not null) File.WriteAllText(dir + ".owner", claim.Format() + "\n");
        return dir;
    }
}
