using System.Text.Json;
using FluentAssertions;
using Mcp.Contracts;
using Mcp.Tests.Leftovers;
using Microsoft.Extensions.Logging.Abstractions;
using Workspace.Application;
using Workspace.Infrastructure;
using Xunit;

namespace Mcp.Tests;

/// <summary>The one real tool, covering what actually carries risk: the sandbox.</summary>
public sealed class WorkspaceToolTests : IDisposable
{
    private readonly OwnedScope _owned = new();

    public void Dispose() => _owned.Dispose();

    [Fact]
    public async Task Reads_a_file_and_reports_the_line_span()
    {
        var (provider, root) = Build();
        await File.WriteAllTextAsync(Path.Combine(root, "a.txt"), "one\ntwo\nthree", TestContext.Current.CancellationToken);

        var result = await Invoke(provider, """{"path":"a.txt"}""");

        result.Should().BeOfType<ToolResult.Ok>()
            .Which.Content.Should().StartWith("lines 1-3 of 3").And.Contain("two");
    }

    [Fact]
    public async Task Reads_a_line_window_by_number()
    {
        var (provider, root) = Build();
        await File.WriteAllTextAsync(Path.Combine(root, "a.txt"), "1\n2\n3\n4\n5", TestContext.Current.CancellationToken);

        var result = await Invoke(provider, """{"path":"a.txt","startLine":2,"lineCount":2}""");

        result.Should().BeOfType<ToolResult.Ok>()
            .Which.Content.Should().Be("lines 2-3 of 5\n2\n3");
    }

    [Fact]
    public async Task A_start_past_the_end_returns_the_real_total_instead_of_an_error()
    {
        var (provider, root) = Build();
        await File.WriteAllTextAsync(Path.Combine(root, "a.txt"), "1\n2", TestContext.Current.CancellationToken);

        var result = await Invoke(provider, """{"path":"a.txt","startLine":99}""");

        // So the caller can correct the offset by number rather than guessing.
        result.Should().BeOfType<ToolResult.Ok>()
            .Which.Content.Should().Contain("of 2");
    }

    [Fact]
    public async Task A_line_count_of_int_max_reads_to_the_end_instead_of_overflowing_into_a_crash()
    {
        var (provider, root) = Build();
        await File.WriteAllTextAsync(Path.Combine(root, "a.txt"), "1\n2\n3\n4\n5", TestContext.Current.CancellationToken);

        // The window arithmetic is `start + count - 1`. In int, with the count any client may send in
        // ONE call, that wraps NEGATIVE, Math.Min picks the negative, and the range indexer throws
        // where nothing above catches. "Everything from line 2" is a legitimate request — a pager
        // sends it — so the answer is the rest of the file, not a refusal and never an exception.
        var result = await Invoke(provider, """{"path":"a.txt","startLine":2,"lineCount":2147483647}""");

        result.Should().BeOfType<ToolResult.Ok>()
            .Which.Content.Should().Be("lines 2-5 of 5\n2\n3\n4\n5");
    }

    [Fact]
    public async Task A_negative_line_count_is_refused_with_the_legal_range_rather_than_read_as_to_the_end()
    {
        var (provider, root) = Build();
        await File.WriteAllTextAsync(Path.Combine(root, "a.txt"), "1\n2\n3", TestContext.Current.CancellationToken);

        var result = await Invoke(provider, """{"path":"a.txt","startLine":1,"lineCount":-5}""");

        // Nonsense is answered with the legal range, not folded into the documented meaning of 0.
        // Reading -5 as "to the end" tells the caller their number was accepted when it was discarded.
        result.Should().BeOfType<ToolResult.Refused>()
            .Which.Reason.Should().Contain("lineCount").And.Contain("2147483647");
    }

    [Fact]
    public async Task A_start_line_too_large_for_an_int32_is_refused_rather_than_silently_read_from_the_top()
    {
        var (provider, root) = Build();
        await File.WriteAllTextAsync(Path.Combine(root, "a.txt"), "1\n2\n3", TestContext.Current.CancellationToken);

        var result = await Invoke(provider, """{"path":"a.txt","startLine":999999999999}""");

        // The JSON boundary must not read a number it cannot hold as the DEFAULT: "from the top" is
        // the one answer that looks successful while being the opposite of what was asked.
        result.Should().BeOfType<ToolResult.Refused>()
            .Which.Reason.Should().Contain("startLine");
    }

    [Theory]
    [InlineData("\"5\"")]
    [InlineData("3.5")]
    [InlineData("true")]
    [InlineData("null")]
    public async Task A_window_number_that_is_not_a_number_is_REFUSED_and_never_thrown(string startLine)
    {
        // Found 2026-08-19 from the sibling repository, where the same shape was written and its own test
        // caught it. JsonElement.TryGetInt32 does NOT return false for a value that is not a number — it
        // THROWS InvalidOperationException, and returns false only for a Number too large for an Int32. So
        // {"startLine": "5"} left this provider as an exception, which the catalog records as a FAILURE:
        // the server broke, rather than the caller being told which argument it got wrong.
        //
        // The doc comment on ReadInt named exactly these inputs as Unreadable. Two of the three behaved as
        // documented; the quoted one did not, which is the more ordinary kind of wrong — a comment that is
        // right about the cases somebody tried.
        var (provider, root) = Build();
        await File.WriteAllTextAsync(Path.Combine(root, "a.txt"), "1\n2\n3", TestContext.Current.CancellationToken);

        var result = await Invoke(provider, $$"""{"path":"a.txt","startLine":{{startLine}}}""");

        result.Should().BeOfType<ToolResult.Refused>()
            .Which.Reason.Should().Contain("startLine");
    }

    [Fact]
    public async Task A_read_of_a_file_larger_than_the_cap_says_it_was_truncated_and_names_the_real_total()
    {
        var (provider, root) = Build();
        await File.WriteAllLinesAsync(
            Path.Combine(root, "big.txt"), Numbered(6_000), TestContext.Current.CancellationToken);

        var result = await Invoke(provider, """{"path":"big.txt"}""");

        // "Omit lineCount to read to the end" cannot mean "hand back whatever is on disk": one large
        // file in the workspace is then a multi-hundred-megabyte allocation AND an equally large single
        // content block over stdio. The cap alone is only half the fix — a truncation the caller cannot
        // SEE is worse than no cap at all, because a short answer reads as a short file and paging
        // stops there. So the answer must carry the TRUE total and the number to page from.
        var content = result.Should().BeOfType<ToolResult.Ok>().Which.Content;
        content.Should().StartWith("lines 1-5000 of 6000");
        content.Should().Contain("TRUNCATED").And.Contain("5001");
        content.Should().EndWith("line 5000");
    }

    [Fact]
    public async Task A_single_line_longer_than_the_byte_cap_is_clipped_instead_of_going_out_whole()
    {
        var (provider, root) = Build();

        // A line cap alone bounds nothing: minified JavaScript, a one-line JSON document and a base64
        // blob are all ONE line, and one line of two megabytes is two megabytes on the wire.
        await File.WriteAllTextAsync(
            Path.Combine(root, "minified.js"), new string('x', 2 * 1024 * 1024), TestContext.Current.CancellationToken);

        var result = await Invoke(provider, """{"path":"minified.js"}""");

        var content = result.Should().BeOfType<ToolResult.Ok>().Which.Content;
        content.Should().Contain("TRUNCATED");
        content.Length.Should().BeLessThan(2 * 1024 * 1024);

        // The one case where "ask for the next window" would be a lie — the rest is INSIDE a line, and
        // no startLine reaches it. Saying so is the difference between a contract and advice.
        content.Should().StartWith(
            "lines 1-1 of 1 — TRUNCATED: line 1 alone is longer than this server's 1048576-byte read "
            + "cap, and 1048576 further byte(s) of that one line were dropped. Paging cannot reach "
            + "them — narrow the request another way.\n");
    }

    [Fact]
    public async Task A_window_that_reaches_the_byte_cap_first_stops_on_a_whole_line_and_says_where()
    {
        var (provider, root) = Build(new SandboxedFileReaderOptions { MaxLines = 100, MaxBytes = 20 });
        await File.WriteAllLinesAsync(
            Path.Combine(root, "a.txt"), Numbered(10), TestContext.Current.CancellationToken);

        var result = await Invoke(provider, """{"path":"a.txt"}""");

        // Two caps, and the tighter one wins. Stopping BEFORE the line that would cross the budget is
        // what keeps the next startLine a whole line — a byte cap that cut mid-line would hand back a
        // number that skips the remainder of the line it cut.
        result.Should().BeOfType<ToolResult.Ok>()
            .Which.Content.Should().Be(
                "lines 1-2 of 10 — TRUNCATED: the window stops at this server's 20-byte read cap. "
                + "Ask again with startLine 3 to continue.\nline 1\nline 2");
    }

    [Fact]
    public async Task A_ten_line_window_is_ten_lines_however_many_lines_the_file_has()
    {
        var (provider, root) = Build();
        await File.WriteAllLinesAsync(
            Path.Combine(root, "huge.txt"), Numbered(200_000), TestContext.Current.CancellationToken);

        var result = await Invoke(provider, """{"path":"huge.txt","startLine":100,"lineCount":10}""");

        // What this DOES pin: a window the caller asked for is served whole out of a file two hundred
        // thousand lines long, with the real total, and is NOT reported as truncated — the caps must
        // bound the runaway read without clipping an ordinary one.
        // What it does NOT prove: that the reader streamed rather than materialized the file. That
        // difference is peak LIVE memory during an async read, and the only faithful assertion for it
        // is a sampling one. Said plainly here rather than dressed up as a guarantee.
        var content = result.Should().BeOfType<ToolResult.Ok>().Which.Content;
        content.Should().Be("lines 100-109 of 200000\n" + string.Join('\n', Numbered(109).Skip(99)));
        content.Should().NotContain("TRUNCATED");
    }

    [Fact]
    public async Task A_host_may_lower_the_caps_and_the_truncated_answer_still_pages_by_number()
    {
        var (provider, root) = Build(new SandboxedFileReaderOptions { MaxLines = 3 });
        await File.WriteAllLinesAsync(
            Path.Combine(root, "a.txt"), Numbered(10), TestContext.Current.CancellationToken);

        var first = await Invoke(provider, """{"path":"a.txt"}""");
        var next = await Invoke(provider, """{"path":"a.txt","startLine":4}""");

        // The point of naming the next startLine: following it must actually WORK. A truncation marker
        // that recommends a window the reader then refuses would be advice, not a contract.
        first.Should().BeOfType<ToolResult.Ok>()
            .Which.Content.Should().Be(
                "lines 1-3 of 10 — TRUNCATED: the window stops at this server's 3-line read cap. "
                + "Ask again with startLine 4 to continue.\nline 1\nline 2\nline 3");
        next.Should().BeOfType<ToolResult.Ok>()
            .Which.Content.Should().StartWith("lines 4-6 of 10").And.EndWith("line 6");
    }

    [Fact]
    public async Task A_window_the_caller_asked_for_is_never_reported_as_truncated()
    {
        var (provider, root) = Build(new SandboxedFileReaderOptions { MaxLines = 3 });
        await File.WriteAllLinesAsync(
            Path.Combine(root, "a.txt"), Numbered(10), TestContext.Current.CancellationToken);

        var result = await Invoke(provider, """{"path":"a.txt","startLine":2,"lineCount":3}""");

        // Three of ten lines, because three is what was ASKED for. Marking that "truncated" would cry
        // wolf on every ordinary paged read, and a marker that fires always tells a reader nothing.
        result.Should().BeOfType<ToolResult.Ok>()
            .Which.Content.Should().Be("lines 2-4 of 10\nline 2\nline 3\nline 4");
    }

    [Fact]
    public void A_cap_that_could_never_return_anything_stops_the_host_instead_of_every_read()
    {
        // At composition, where one message is read once — not as an empty answer on every read for
        // the life of a process nobody is watching.
        var build = () => new SandboxedFileReader(
            // testsToDelete-exempt: raw-temp — a read-only use, the reader is only built and nothing is made in the temp root
            new WorkspaceRoot(Owned.TempRoot()),
            new SandboxedFileReaderOptions { MaxBytes = 0 },
            NullLogger<SandboxedFileReader>.Instance);

        build.Should().Throw<InvalidOperationException>().WithMessage("*MaxBytes*");
    }

    private static IEnumerable<string> Numbered(int count) =>
        Enumerable.Range(1, count).Select(i => $"line {i}");

    [Theory]
    [InlineData("../outside.txt")]
    [InlineData("sub/../../outside.txt")]
    public async Task Refuses_to_escape_the_workspace_root(string path)
    {
        var (provider, root) = Build();
        await File.WriteAllTextAsync(
            Path.Combine(Directory.GetParent(root)!.FullName, "outside.txt"), "secret", TestContext.Current.CancellationToken);

        var result = await Invoke(provider, $$"""{"path":"{{path}}"}""");

        // A refusal, never an empty read — the failure mode that makes a breach look like a missing
        // file. And a REFUSAL rather than a failure: the guard worked, which is the opposite of the
        // disk breaking, and a ledger that files both under "error" cannot count either.
        result.Should().BeOfType<ToolResult.Refused>()
            .Which.Reason.Should().Contain("outside the workspace");
    }

    [Fact]
    public void The_escape_target_is_written_inside_an_owned_folder_never_into_the_temp_root()
    {
        var (_, root) = Build();

        var parent = Directory.GetParent(root)!.FullName;

        Owned.Pattern().IsMatch(Path.GetFileName(parent)).Should().BeTrue($"{parent} must be an owned folder, not the temp root");
    }

    [Fact]
    public async Task Refuses_an_absolute_path()
    {
        var (provider, _) = Build();

        // testsToDelete-exempt: raw-temp — a read-only use, the temp root is only the absolute path to refuse
        var result = await Invoke(provider, $$"""{"path":"{{Owned.TempRoot().Replace("\\", "\\\\")}}"}""");

        result.Should().BeOfType<ToolResult.Refused>();
    }

    [Fact]
    public async Task Refuses_to_read_through_a_directory_link_that_points_outside_the_root()
    {
        var (provider, root) = Build();
        var outside = _owned.Dir("outside");
        await File.WriteAllTextAsync(Path.Combine(outside, "secret.txt"), "secret", TestContext.Current.CancellationToken);
        DirectoryLinks.CreateOrSkip(Path.Combine(root, "vendor"), outside);

        // The path SPELLS fine — no "..", no absolute — and a lexical check waves it through while the
        // read follows the link transparently. The real target is what must lie under the root.
        var result = await Invoke(provider, """{"path":"vendor/secret.txt"}""");

        result.Should().BeOfType<ToolResult.Refused>()
            .Which.Reason.Should().Contain("outside the workspace");
    }

    [Fact]
    public async Task Refuses_a_file_link_whose_target_lies_outside_the_root()
    {
        var (provider, root) = Build();
        var outside = _owned.Dir("outside");
        var target = Path.Combine(outside, "secret.txt");
        await File.WriteAllTextAsync(target, "secret", TestContext.Current.CancellationToken);
        CreateLinkOrSkip(() => File.CreateSymbolicLink(Path.Combine(root, "alias.txt"), target));

        var result = await Invoke(provider, """{"path":"alias.txt"}""");

        result.Should().BeOfType<ToolResult.Refused>()
            .Which.Reason.Should().Contain("outside the workspace");
    }

    [Fact]
    public async Task A_link_that_stays_inside_the_root_still_reads()
    {
        var (provider, root) = Build();
        await File.WriteAllTextAsync(Path.Combine(root, "sub", "real.txt"), "real", TestContext.Current.CancellationToken);
        DirectoryLinks.CreateOrSkip(Path.Combine(root, "alias"), Path.Combine(root, "sub"));

        // The guard is about where the TARGET lives, not about links as such — a checkout may
        // legitimately link within itself, and refusing that would punish the repository for its layout.
        var result = await Invoke(provider, """{"path":"alias/real.txt"}""");

        result.Should().BeOfType<ToolResult.Ok>()
            .Which.Content.Should().Contain("real");
    }

    [Theory]
    [InlineData("alias", "l2", "alias/secret.txt")]
    [InlineData("alias2", "l2/secret.txt", "alias2")]
    [InlineData("alias3", "l2/deeper", "alias3/secret2.txt")]
    public async Task Refuses_a_link_that_reaches_the_outside_through_a_second_link_inside_the_root(
        string alias, string aliasTarget, string path)
    {
        var (provider, root) = Build();
        var outside = _owned.Dir("outside");
        Directory.CreateDirectory(Path.Combine(outside, "deeper"));
        await File.WriteAllTextAsync(Path.Combine(outside, "secret.txt"), "secret", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(outside, "deeper", "secret2.txt"), "secret2", TestContext.Current.CancellationToken);
        DirectoryLinks.CreateOrSkip(Path.Combine(root, "l2"), outside);

        // The alias is a link whose TARGET is spelled through another link of the root. The target's own
        // directories were never resolved, so the spelling `<root>/l2/...` passed the prefix check while
        // the read followed `l2` out of the root. Three shapes: a directory link to the second link, a
        // file link through it, and a directory link to a folder beneath it.
        var target = Path.Combine(root, aliasTarget.Replace('/', Path.DirectorySeparatorChar));
        if (aliasTarget.EndsWith(".txt", StringComparison.Ordinal))
        {
            CreateLinkOrSkip(() => File.CreateSymbolicLink(Path.Combine(root, alias), target));
        }
        else
        {
            DirectoryLinks.CreateOrSkip(Path.Combine(root, alias), target);
        }

        var result = await Invoke(provider, $$"""{"path":"{{path}}"}""");

        // Said first so a breach reads as what it is: the secret came back.
        (result as ToolResult.Ok)?.Content.Should().NotContain("secret", "the read followed a link out of the root");
        result.Should().BeOfType<ToolResult.Refused>()
            .Which.Reason.Should().Contain("outside the workspace");
    }

    [Fact]
    public async Task A_link_spelled_through_a_link_in_the_root_own_path_still_reads()
    {
        var scratch = _owned.Dir("scratch");
        var realDir = Path.Combine(scratch, "realDir");
        Directory.CreateDirectory(realDir);
        var viaLink = Path.Combine(scratch, "viaLink");
        DirectoryLinks.CreateOrSkip(viaLink, realDir);
        var root = Path.Combine(viaLink, "root");
        Directory.CreateDirectory(Path.Combine(root, "sub"));
        await File.WriteAllTextAsync(Path.Combine(root, "sub", "real.txt"), "real", TestContext.Current.CancellationToken);
        DirectoryLinks.CreateOrSkip(Path.Combine(root, "alias"), Path.Combine(viaLink, "root", "sub"));
        var provider = ProviderAt(root);

        // The machine where this was measured (macOS CI, /var -> /private/var) spells every temp path
        // through a link. The alias target is spelled through the root's own link; resolving only the
        // alias left that spelling unresolved and the prefix check against the real root refused a read
        // that stays inside the root.
        var result = await Invoke(provider, """{"path":"alias/real.txt"}""");

        result.Should().BeOfType<ToolResult.Ok>()
            .Which.Content.Should().Contain("real");
    }

    [Fact]
    public async Task A_relative_link_that_climbs_out_of_the_root_with_dot_dot_is_refused()
    {
        var (provider, root) = Build();
        var outside = _owned.Dir("outside");
        var secret = Path.Combine(outside, "secret.txt");
        await File.WriteAllTextAsync(secret, "secret", TestContext.Current.CancellationToken);
        CreateLinkOrSkip(() => File.CreateSymbolicLink(Path.Combine(root, "up.txt"), Path.GetRelativePath(root, secret)));

        var result = await Invoke(provider, """{"path":"up.txt"}""");

        (result as ToolResult.Ok)?.Content.Should().NotContain("secret", "the read followed a relative link out of the root");
        result.Should().BeOfType<ToolResult.Refused>()
            .Which.Reason.Should().Contain("outside the workspace");
    }

    [Fact]
    public async Task A_relative_link_that_stays_inside_the_root_reads()
    {
        var (provider, root) = Build();
        await File.WriteAllTextAsync(Path.Combine(root, "sub", "real.txt"), "real", TestContext.Current.CancellationToken);
        DirectoryLinks.CreateOrSkip(Path.Combine(root, "alias"), Path.Combine("sub", "..", "sub"));

        var result = await Invoke(provider, """{"path":"alias/real.txt"}""");

        result.Should().BeOfType<ToolResult.Ok>()
            .Which.Content.Should().Contain("real");
    }

    [Fact]
    public async Task A_link_loop_is_refused_promptly_and_never_hangs()
    {
        var (provider, root) = Build();
        CreateLinkOrSkip(() => File.CreateSymbolicLink(Path.Combine(root, "a"), "b"));
        CreateLinkOrSkip(() => File.CreateSymbolicLink(Path.Combine(root, "b"), "a"));
        var clock = System.Diagnostics.Stopwatch.StartNew();

        var result = await Invoke(provider, """{"path":"a"}""");

        // Forty-one expansions and the budget is spent: the guard answers, it does not walk forever.
        result.Should().BeOfType<ToolResult.Refused>()
            .Which.Reason.Should().Contain("outside the workspace");
        clock.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(10));
    }

    /// <summary>The budget of the system this runs on, and one past it: the chain test reads the constant, never a literal.</summary>
    public static TheoryData<int, bool> ChainsAroundTheBudget => new()
    {
        { SandboxedFileReader.MaxLinkHops, true },
        { SandboxedFileReader.MaxLinkHops + 1, false },
    };

    [Fact]
    public void The_link_budget_is_the_kernels_limit_on_macOS_32_and_40_elsewhere()
    {
        // macOS's MAXSYMLINKS is 32: a chain of 40 fails open(2) with ELOOP there, so the resolver must refuse it first.
        // Windows allows 63 and Linux 40, so 40 (the stricter one for Windows) stays everywhere else.
        SandboxedFileReader.LinkHopBudget(isMacOs: true).Should().Be(32);
        SandboxedFileReader.LinkHopBudget(isMacOs: false).Should().Be(40);
        SandboxedFileReader.MaxLinkHops.Should().Be(SandboxedFileReader.LinkHopBudget(OperatingSystem.IsMacOS()));
    }

    [Theory]
    [MemberData(nameof(ChainsAroundTheBudget))]
    public async Task A_chain_of_links_reads_up_to_the_hop_budget_and_is_refused_one_past_it(int hops, bool reads)
    {
        var (provider, root) = Build();
        await File.WriteAllTextAsync(Path.Combine(root, "real.txt"), "real", TestContext.Current.CancellationToken);

        // link1 -> link2 -> ... -> link{hops} -> real.txt: reading link1 expands exactly `hops` links.
        for (var i = 1; i <= hops; i++)
        {
            var next = i == hops ? "real.txt" : $"link{i + 1}";
            CreateLinkOrSkip(() => File.CreateSymbolicLink(Path.Combine(root, $"link{i}"), next));
        }

        var result = await Invoke(provider, """{"path":"link1"}""");

        if (reads)
        {
            result.Should().BeOfType<ToolResult.Ok>().Which.Content.Should().Contain("real");
        }
        else
        {
            result.Should().BeOfType<ToolResult.Refused>().Which.Reason.Should().Contain("outside the workspace");
        }
    }

    [Fact]
    public async Task A_chain_of_the_full_budget_reads_when_the_root_itself_is_spelled_through_a_link()
    {
        // macOS's temp folder is /var/folders/..., and /var is a link to /private/var: a request resolved through the root's
        // SPELLING spent one hop of its budget on the root's own link, so a chain of exactly the budget was refused there.
        // The root's links are resolved once, when the reader is made; a request spends the budget only below it.
        var home = _owned.Dir("aliased");
        var realRoot = Path.Combine(home, "real-root");
        Directory.CreateDirectory(realRoot);
        var alias = Path.Combine(home, "alias");
        CreateLinkOrSkip(() => Directory.CreateSymbolicLink(alias, realRoot));
        await File.WriteAllTextAsync(Path.Combine(realRoot, "real.txt"), "real", TestContext.Current.CancellationToken);
        var hops = SandboxedFileReader.MaxLinkHops;
        for (var i = 1; i <= hops; i++)
        {
            var next = i == hops ? "real.txt" : $"link{i + 1}";
            CreateLinkOrSkip(() => File.CreateSymbolicLink(Path.Combine(realRoot, $"link{i}"), next));
        }

        var result = await Invoke(ProviderAt(alias), """{"path":"link1"}""");

        result.Should().BeOfType<ToolResult.Ok>("the root's own link is not the request's to pay for")
            .Which.Content.Should().Contain("real");
    }

    [Fact]
    public async Task A_deep_path_of_plain_directories_reads_because_only_links_spend_the_budget()
    {
        var (provider, root) = Build();
        var parts = Enumerable.Range(0, SandboxedFileReader.MaxLinkHops + 20).Select(i => $"d{i}").ToArray();
        var deep = Path.Combine([root, .. parts]);
        Directory.CreateDirectory(deep);
        await File.WriteAllTextAsync(Path.Combine(deep, "f.txt"), "deep", TestContext.Current.CancellationToken);

        var result = await Invoke(provider, $$"""{"path":"{{string.Join('/', parts)}}/f.txt"}""");

        result.Should().BeOfType<ToolResult.Ok>()
            .Which.Content.Should().Contain("deep");
    }

    [Fact]
    public async Task A_chained_junction_that_reaches_the_outside_is_refused()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("a junction is an NTFS reparse point; the symlink twin of this chain runs on every OS (alias2/alias3 above)");
        }

        var (provider, root) = Build();
        var outside = _owned.Dir("outside");
        await File.WriteAllTextAsync(Path.Combine(outside, "secret.txt"), "secret", TestContext.Current.CancellationToken);
        DirectoryLinks.CreateOrSkip(Path.Combine(root, "j2"), outside);
        DirectoryLinks.CreateOrSkip(Path.Combine(root, "j1"), Path.Combine(root, "j2"));

        var result = await Invoke(provider, """{"path":"j1/secret.txt"}""");

        (result as ToolResult.Ok)?.Content.Should().NotContain("secret", "the read followed a chained junction out of the root");
        result.Should().BeOfType<ToolResult.Refused>()
            .Which.Reason.Should().Contain("outside the workspace");
    }

    [Fact]
    public async Task What_is_opened_is_the_resolved_path_that_was_checked_and_never_the_link_spelling()
    {
        var home = _owned.Dir("workspace");
        var root = Path.Combine(home, "root");
        Directory.CreateDirectory(Path.Combine(root, "sub"));
        await File.WriteAllTextAsync(Path.Combine(root, "sub", "real.txt"), "real", TestContext.Current.CancellationToken);
        DirectoryLinks.CreateOrSkip(Path.Combine(root, "alias"), Path.Combine(root, "sub"));
        var opened = new List<string>();
        var reader = new SandboxedFileReader(
            new WorkspaceRoot(root),
            new SandboxedFileReaderOptions(),
            NullLogger<SandboxedFileReader>.Instance,
            (path, token) =>
            {
                opened.Add(path);
                return File.ReadLinesAsync(path, token);
            });

        var outcome = await reader.ReadAsync(new FileReadRequest("alias/real.txt", 0, 0), TestContext.Current.CancellationToken);

        outcome.Should().BeOfType<FileReadOutcome.Ok>();
        var path = opened.Should().ContainSingle().Which;
        path.Should().EndWith(Path.Combine("sub", "real.txt"));
        path.Should().NotContain("alias");
        // No segment of what was opened is a link: nothing is left for the open to follow.
        for (var dir = Path.GetDirectoryName(path); !string.IsNullOrEmpty(dir); dir = Path.GetDirectoryName(dir))
        {
            new DirectoryInfo(dir).LinkTarget.Should().BeNull($"{dir} is part of the opened path");
        }
    }

    [Theory]
    [InlineData("a", "nonexistent/../evil", "a")]
    [InlineData("a2", "zz/..", "a2/evil")]
    public async Task Refuses_a_dot_dot_after_a_missing_segment_because_the_kernel_answers_not_found_there(
        string alias, string aliasTarget, string path)
    {
        var (provider, root) = Build();
        var outside = _owned.Dir("outside");
        var secret = Path.Combine(outside, "secret.txt");
        await File.WriteAllTextAsync(secret, "secret", TestContext.Current.CancellationToken);
        CreateLinkOrSkip(() => File.CreateSymbolicLink(Path.Combine(root, "evil"), secret));
        CreateLinkOrSkip(() => File.CreateSymbolicLink(Path.Combine(root, alias), aliasTarget.Replace('/', Path.DirectorySeparatorChar)));

        // `nonexistent/..` is NOT the directory beside it: opening it fails with "not found". A resolver
        // that popped the missing segment lexically landed on `<root>/evil`, then stopped looking at links.
        var result = await Invoke(provider, $$"""{"path":"{{path}}"}""");

        (result as ToolResult.Ok)?.Content.Should().NotContain("secret", "the read followed a lexical dot-dot out of the root");
        result.Should().BeOfType<ToolResult.Refused>();
    }

    [Fact]
    public async Task A_path_of_two_hundred_thousand_segments_is_refused_at_once_and_the_refusal_stays_short()
    {
        var (provider, _) = Build();
        var huge = string.Concat(Enumerable.Repeat("a/", 200_000)) + "x";
        var clock = System.Diagnostics.Stopwatch.StartNew();

        var result = await Invoke(provider, $$"""{"path":"{{huge}}"}""");

        // The walk was quadratic in the number of segments (a string rebuilt per segment): 36 s measured.
        clock.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(2));
        var refused = result.Should().BeOfType<ToolResult.Refused>().Which;
        refused.Reason.Length.Should().BeLessThan(400, "the path is echoed truncated, not whole");
    }

    [Fact]
    public async Task A_NUL_in_the_path_is_a_refusal_and_not_a_failure()
    {
        var (provider, _) = Build();

        var result = await Invoke(provider, """{"path":"a\u0000b"}""");

        result.Should().BeOfType<ToolResult.Refused>();
    }

    [Fact]
    public async Task Refuses_a_dot_dot_after_an_existing_file_because_the_kernel_answers_not_a_directory()
    {
        var (provider, root) = Build();
        await File.WriteAllTextAsync(Path.Combine(root, "a.txt"), "one", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(root, "b.txt"), "bee", TestContext.Current.CancellationToken);
        CreateLinkOrSkip(() => File.CreateSymbolicLink(
            Path.Combine(root, "f2"), Path.Combine("a.txt", "..", "b.txt")));

        // `a.txt/..` is not the directory beside the file: opening it fails ("not a directory").
        var result = await Invoke(provider, """{"path":"f2"}""");

        (result as ToolResult.Ok)?.Content.Should().NotContain("bee", "a dot-dot above a FILE was popped textually");
        result.Should().BeOfType<ToolResult.Refused>();
    }

    [Fact]
    public async Task A_path_with_a_NUL_is_echoed_with_the_control_character_escaped_in_the_reason_and_the_log()
    {
        var home = _owned.Dir("workspace");
        var root = Path.Combine(home, "root");
        Directory.CreateDirectory(root);
        var log = new RecordingLogger<SandboxedFileReader>();
        var reader = new SandboxedFileReader(new WorkspaceRoot(root), new SandboxedFileReaderOptions(), log);

        var outcome = await reader.ReadAsync(new FileReadRequest("a\0b\n", 0, 0), TestContext.Current.CancellationToken);

        var reason = outcome.Should().BeOfType<FileReadOutcome.Refused>().Which.Reason;
        reason.Should().Contain("a\\0b\\n").And.NotContain("\0").And.NotContain("\n");
        var warning = log.Warnings.Should().ContainSingle().Which;
        warning.Should().Contain("a\\0b\\n").And.NotContain("\0").And.NotContain("\n");
    }

    [Fact]
    public async Task A_capped_read_logs_the_path_truncated_not_whole()
    {
        var home = _owned.Dir("workspace");
        var root = Path.Combine(home, "root");
        var parts = Enumerable.Range(0, 30).Select(i => $"directory{i:D3}").ToArray();
        var deep = Path.Combine([root, .. parts]);
        Directory.CreateDirectory(deep);
        await File.WriteAllLinesAsync(Path.Combine(deep, "f.txt"), Numbered(10), TestContext.Current.CancellationToken);
        var log = new RecordingLogger<SandboxedFileReader>();
        var reader = new SandboxedFileReader(new WorkspaceRoot(root), new SandboxedFileReaderOptions { MaxLines = 3 }, log);
        var requested = string.Join('/', parts) + "/f.txt";

        var outcome = await reader.ReadAsync(new FileReadRequest(requested, 0, 0), TestContext.Current.CancellationToken);

        outcome.Should().BeOfType<FileReadOutcome.Ok>();
        var line = log.Infos.Should().ContainSingle().Which;
        line.Should().NotContain(requested).And.Contain("…");
    }

    [Theory]
    [InlineData("""{}""")]
    [InlineData("""{"path":null}""")]
    [InlineData("""{"path":""}""")]
    public async Task A_missing_null_or_empty_path_is_a_refusal_through_the_tool(string arguments)
    {
        var (provider, _) = Build();

        var result = await Invoke(provider, arguments);

        result.Should().BeOfType<ToolResult.Refused>();
    }

    [Fact]
    public async Task A_null_path_reaching_the_reader_is_a_refusal_and_never_a_null_reference()
    {
        var home = _owned.Dir("workspace");
        var root = Path.Combine(home, "root");
        Directory.CreateDirectory(root);
        var reader = new SandboxedFileReader(
            new WorkspaceRoot(root), new SandboxedFileReaderOptions(), NullLogger<SandboxedFileReader>.Instance);

        var outcome = await reader.ReadAsync(new FileReadRequest(null!, 0, 0), TestContext.Current.CancellationToken);

        outcome.Should().BeOfType<FileReadOutcome.Refused>();
    }

    [Fact]
    public async Task A_rooted_link_target_without_a_drive_resolves_on_the_links_own_volume()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("a target such as \\sub (rooted, no drive) exists only on Windows");
        }

        var (provider, root) = Build();
        var volumeRoot = Path.GetPathRoot(root)!;
        var inRoot = Path.DirectorySeparatorChar + Path.GetRelativePath(volumeRoot, Path.Combine(root, "sub"));
        await File.WriteAllTextAsync(Path.Combine(root, "sub", "real.txt"), "real", TestContext.Current.CancellationToken);
        DirectoryLinks.CreateOrSkip(Path.Combine(root, "alias"), inRoot);
        DirectoryLinks.CreateOrSkip(Path.Combine(root, "drive"), "C:sub");

        // `\x` means the root of the link's OWN drive, never the process's current drive; `C:x` is
        // relative to a per-drive working directory and is refused outright.
        (await Invoke(provider, """{"path":"alias/real.txt"}""")).Should().BeOfType<ToolResult.Ok>();
        (await Invoke(provider, """{"path":"drive/real.txt"}""")).Should().BeOfType<ToolResult.Refused>();
    }

    /// <summary>Creating a symlink on Windows needs Developer Mode or elevation. A run without either
    /// SKIPS these tests loudly rather than passing them vacuously — an escape test that never created
    /// its link proves nothing about the guard.</summary>
    private static void CreateLinkOrSkip(Action create)
    {
        try
        {
            create();
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            Assert.Skip($"cannot create symbolic links here ({ex.Message}); enable Developer Mode to run this test");
        }
    }

    [Fact]
    public async Task A_missing_path_argument_is_a_readable_refusal()
    {
        var (provider, _) = Build();

        var result = await Invoke(provider, """{}""");

        result.Should().BeOfType<ToolResult.Refused>()
            .Which.Reason.Should().Contain("'path' is required");
    }

    [Fact]
    public void The_provider_reports_which_workspace_it_is_scoped_to()
    {
        var (provider, root) = Build();

        // "A file was read" is half a fact; the other half is which tree it was read from.
        provider.Scope.Should().Be(root);
    }

    private static Task<ToolResult> Invoke(WorkspaceToolProvider provider, string argumentsJson) =>
        provider.InvokeAsync(
            new ToolCall(WorkspaceToolProvider.ReadLocalFile, JsonDocument.Parse(argumentsJson).RootElement.Clone()),
            TestContext.Current.CancellationToken);

    private (WorkspaceToolProvider Provider, string Root) Build(SandboxedFileReaderOptions? caps = null)
    {
        // The workspace root is a child of an OWNED folder, so whatever a test writes beside the root (the escape target of
        // Refuses_to_escape_the_workspace_root) lands in `home`, dies with it, and is its own per theory row.
        var home = _owned.Dir("workspace");
        var root = Path.Combine(home, "root");
        Directory.CreateDirectory(Path.Combine(root, "sub"));
        return (ProviderAt(root, caps), root);
    }

    private static WorkspaceToolProvider ProviderAt(string root, SandboxedFileReaderOptions? caps = null) =>
        new(new SandboxedFileReader(
            new WorkspaceRoot(root), caps ?? new SandboxedFileReaderOptions(), NullLogger<SandboxedFileReader>.Instance));
}
