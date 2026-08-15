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
