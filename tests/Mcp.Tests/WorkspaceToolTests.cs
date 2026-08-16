using System.Text.Json;
using FluentAssertions;
using Mcp.Contracts;
using Microsoft.Extensions.Logging.Abstractions;
using Workspace.Application;
using Workspace.Infrastructure;
using Xunit;

namespace Mcp.Tests;

/// <summary>The one real tool, covering what actually carries risk: the sandbox.</summary>
public sealed class WorkspaceToolTests
{
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
            new WorkspaceRoot(Path.GetTempPath()),
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
    public async Task Refuses_an_absolute_path()
    {
        var (provider, _) = Build();

        var result = await Invoke(provider, $$"""{"path":"{{Path.GetTempPath().Replace("\\", "\\\\")}}"}""");

        result.Should().BeOfType<ToolResult.Refused>();
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

    private static (WorkspaceToolProvider Provider, string Root) Build(SandboxedFileReaderOptions? caps = null)
    {
        var root = Directory.CreateTempSubdirectory("mcp-workspace").FullName;
        Directory.CreateDirectory(Path.Combine(root, "sub"));
        var reader = new SandboxedFileReader(
            new WorkspaceRoot(root), caps ?? new SandboxedFileReaderOptions(), NullLogger<SandboxedFileReader>.Instance);
        return (new WorkspaceToolProvider(reader), root);
    }
}
