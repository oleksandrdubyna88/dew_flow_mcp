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

    private static (WorkspaceToolProvider Provider, string Root) Build()
    {
        var root = Directory.CreateTempSubdirectory("mcp-workspace").FullName;
        Directory.CreateDirectory(Path.Combine(root, "sub"));
        var reader = new SandboxedFileReader(new WorkspaceRoot(root), NullLogger<SandboxedFileReader>.Instance);
        return (new WorkspaceToolProvider(reader), root);
    }
}
