using FluentAssertions;
using Xunit;

namespace Mcp.Tests.Leftovers;

/// <summary>The per-system errno tables: Darwin's numbers are pinned from <c>sys/errno.h</c> and are never the Linux ones.</summary>
public sealed class ErrnoTests
{
    [Fact]
    public void Darwin_has_its_own_numbers_where_it_differs_from_linux()
    {
        var darwin = ErrnoTable.Darwin;

        (darwin.Enoent, darwin.Eexist, darwin.Enotdir, darwin.Eisdir, darwin.Enotempty, darwin.Enotsup, darwin.Einval)
            .Should().Be((2, 17, 20, 21, 66, 45, 22));
        (darwin.Esrch, darwin.Eperm, darwin.Enosys).Should().Be((3, 1, 78));
        (ErrnoTable.Linux.Enotempty, ErrnoTable.Linux.Enotsup, ErrnoTable.Linux.Enosys).Should().Be((39, 95, 38));
        (darwin.Enotempty, darwin.Enotsup, darwin.Enosys).Should().NotBe((ErrnoTable.Linux.Enotempty, ErrnoTable.Linux.Enotsup, ErrnoTable.Linux.Enosys));
    }

    [Fact]
    public void The_current_table_is_the_one_of_the_system_this_runs_on()
    {
        if (OperatingSystem.IsMacOS()) Errno.Current.Should().Be(ErrnoTable.Darwin);
        else Errno.Current.Should().Be(ErrnoTable.Linux);
    }
}
