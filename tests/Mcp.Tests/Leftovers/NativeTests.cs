using System.Buffers.Binary;
using FluentAssertions;
using Xunit;

namespace Mcp.Tests.Leftovers;

/// <summary>The OS calls the contract needs and .NET does not have: an entry's identity (device, file id,
/// kind, change and write times) without following a link, a directory made EXCLUSIVELY, and a rename that
/// refuses to replace what it should not. Everything here reads the entry itself and fails closed.</summary>
public sealed class NativeTests
{
    [Fact]
    public void A_folder_and_a_file_report_their_own_kind_and_a_plausible_time()
    {
        using var scratch = new ScratchDir();
        File.WriteAllText(scratch.File("f.txt"), "x");
        var before = DateTimeOffset.UtcNow.AddMinutes(-2);

        var folder = Native.IdentityOf(scratch.Path)!.Value;
        var file = Native.IdentityOf(scratch.File("f.txt"))!.Value;

        (folder.IsDirectory, folder.IsLink, folder.IsRegular).Should().Be((true, false, false));
        (file.IsDirectory, file.IsLink, file.IsRegular).Should().Be((false, false, true));
        file.WriteTime.Should().BeAfter(before).And.BeBefore(DateTimeOffset.UtcNow.AddMinutes(2));
        file.ChangeTime.Should().BeAfter(before).And.BeBefore(DateTimeOffset.UtcNow.AddMinutes(2));
    }

    [Fact]
    public void The_uid_of_an_entry_is_the_callers_on_linux_and_absent_on_windows()
    {
        using var scratch = new ScratchDir();

        var uid = Native.IdentityOf(scratch.Path)!.Value.Uid;

        if (OperatingSystem.IsWindows()) uid.Should().BeNull();
        else uid.Should().Be(Native.GetUid());
    }

    [Fact]
    public void A_missing_path_has_no_identity()
    {
        using var scratch = new ScratchDir();

        Native.IdentityOf(scratch.File("nope")).Should().BeNull();
    }

    [Fact]
    public void A_folder_is_the_same_entry_after_a_move_and_a_different_one_from_a_new_folder_of_the_old_name()
    {
        using var scratch = new ScratchDir();
        var first = scratch.File("a");
        var moved = scratch.File("b");
        Directory.CreateDirectory(first);
        var before = Native.IdentityOf(first)!.Value;

        Directory.Move(first, moved);
        // The moved folder stays alive while the new one is made: an inode freed first is reused by the next mkdir.
        Directory.CreateDirectory(first);

        var after = Native.IdentityOf(moved)!.Value;
        (after.Dev, after.Ino).Should().Be((before.Dev, before.Ino));
        var recreated = Native.IdentityOf(first)!.Value;
        (recreated.Dev, recreated.Ino).Should().NotBe((before.Dev, before.Ino));
    }

    [Fact]
    public void A_link_is_reported_as_a_link_and_never_followed()
    {
        using var scratch = new ScratchDir();
        Directory.CreateDirectory(scratch.File("real"));
        try
        {
            Directory.CreateSymbolicLink(scratch.File("link"), scratch.File("real"));
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            Assert.Skip($"cannot create directory links here ({ex.Message})");
        }

        var link = Native.IdentityOf(scratch.File("link"))!.Value;
        var real = Native.IdentityOf(scratch.File("real"))!.Value;

        link.IsLink.Should().BeTrue();
        (link.Dev, link.Ino).Should().NotBe((real.Dev, real.Ino), "the link's own entry, not its target");
    }

    [Fact]
    public void A_statx_buffer_is_read_at_the_kernels_fixed_offsets()
    {
        var buffer = new byte[256];
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(0), 0x7ff);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(20), 1234);
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(28), 0x41ED);
        BinaryPrimitives.WriteUInt64LittleEndian(buffer.AsSpan(32), 987654321);
        BinaryPrimitives.WriteInt64LittleEndian(buffer.AsSpan(96), 1_700_000_000);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(104), 500_000_000);
        BinaryPrimitives.WriteInt64LittleEndian(buffer.AsSpan(112), 1_700_000_100);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(136), 8);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(140), 1);

        var identity = Native.ParseStatx(buffer);

        identity.Dev.Should().Be((8UL << 32) | 1);
        identity.Ino.Should().Be((UInt128)987654321);
        identity.Uid.Should().Be(1234u);
        (identity.IsDirectory, identity.IsLink, identity.IsRegular).Should().Be((true, false, false));
        identity.ChangeTime.Should().Be(DateTimeOffset.FromUnixTimeSeconds(1_700_000_000).AddTicks(5_000_000));
        identity.WriteTime.Should().Be(DateTimeOffset.FromUnixTimeSeconds(1_700_000_100));
    }

    [Theory]
    [InlineData(0x100u, "STATX_INO")]
    [InlineData(0x8u, "STATX_UID")]
    [InlineData(0x40u, "STATX_MTIME")]
    [InlineData(0x80u, "STATX_CTIME")]
    [InlineData(0x1u, "STATX_TYPE")]
    [InlineData(0x2u, "STATX_MODE")]
    public void A_statx_answer_without_a_field_this_reads_is_no_identity_and_fails_closed(uint missing, string name)
    {
        var buffer = new byte[256];
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(0), 0x7ff & ~missing);
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(28), 0x41ED);

        var act = () => Native.ParseStatx(buffer);

        act.Should().Throw<InvalidOperationException>($"a file system that answers without {name} gives no identity");
    }

    [Fact]
    public void A_directory_is_made_once_and_a_second_attempt_says_it_exists()
    {
        using var scratch = new ScratchDir();

        Native.MakeDirectoryExclusively(scratch.File("d")).Should().BeTrue();
        Native.MakeDirectoryExclusively(scratch.File("d")).Should().BeFalse("an existing folder is somebody else's: never adopted");
        Directory.Exists(scratch.File("d")).Should().BeTrue();
    }

    [Fact]
    public void A_file_under_the_name_also_says_it_exists_and_a_missing_parent_throws()
    {
        using var scratch = new ScratchDir();
        File.WriteAllText(scratch.File("f"), "x");

        Native.MakeDirectoryExclusively(scratch.File("f")).Should().BeFalse();
        var act = () => Native.MakeDirectoryExclusively(Path.Combine(scratch.Path, "no", "d"));
        act.Should().Throw<IOException>("any failure but 'already exists' is an error, never a quiet false");
    }

    [Fact]
    public void On_linux_the_folder_is_private_to_the_caller()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Skip("a POSIX mode — Linux only");
            return;
        }

        using var scratch = new ScratchDir();

        Native.MakeDirectoryExclusively(scratch.File("d"));

        var others = UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute
            | UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute;
        (File.GetUnixFileMode(scratch.File("d")) & others).Should().Be((UnixFileMode)0, "mode 0700: only the caller's processes read a scratch folder");
    }

    [Fact]
    public void A_folder_is_renamed_onto_a_free_name_and_onto_an_empty_folder_on_linux_only()
    {
        using var scratch = new ScratchDir();
        Directory.CreateDirectory(scratch.File("a"));
        File.WriteAllText(Path.Combine(scratch.File("a"), "keep.txt"), "kept");

        Native.RenameOnto(scratch.File("a"), scratch.File("b"));

        Directory.Exists(scratch.File("a")).Should().BeFalse();
        File.ReadAllText(Path.Combine(scratch.File("b"), "keep.txt")).Should().Be("kept");

        Directory.CreateDirectory(scratch.File("empty"));
        var onto = () => Native.RenameOnto(scratch.File("b"), scratch.File("empty"));
        if (OperatingSystem.IsWindows())
        {
            onto.Should().Throw<IOException>("Directory.Move refuses an existing target, so the restore is exclusive by itself");
        }
        else
        {
            onto.Should().NotThrow("rename(2) onto an EMPTY directory is atomic: the placeholder the restore reserved");
            File.ReadAllText(Path.Combine(scratch.File("empty"), "keep.txt")).Should().Be("kept");
        }
    }
}
