using System.Text.Json;
using FluentAssertions;
using Mcp.Application;
using Mcp.Contracts;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Mcp.Tests;

public sealed class ToolCatalogTests
{
    [Fact]
    public void Two_providers_claiming_one_name_stop_the_host_and_name_both()
    {
        var build = () => Build(new FakeProvider("dup"), new FakeProvider("dup"));

        // Silent shadowing is how a surface starts lying about what it actually runs.
        build.Should().Throw<InvalidOperationException>()
            .WithMessage("*dup*FakeProvider*FakeProvider*");
    }

    [Fact]
    public async Task An_unknown_tool_comes_back_as_a_failure_rather_than_an_exception()
    {
        var catalog = Build(new FakeProvider("known"));

        var result = await catalog.InvokeAsync(new ToolCall("missing", Empty()), TestContext.Current.CancellationToken);

        result.Should().BeOfType<ToolResult.Failed>()
            .Which.Message.Should().Contain("missing");
    }

    [Fact]
    public async Task Every_call_reaches_the_usage_sink_as_its_own_outcome()
    {
        var sink = new RecordingSink();
        var catalog = Build(sink, new FakeProvider("answers"), new RefusingProvider(), new BreakingProvider());

        await catalog.InvokeAsync(new ToolCall("answers", Empty()), TestContext.Current.CancellationToken);
        await catalog.InvokeAsync(new ToolCall("refuses", Empty()), TestContext.Current.CancellationToken);
        await catalog.InvokeAsync(new ToolCall("breaks", Empty()), TestContext.Current.CancellationToken);

        // Three states, not two. A tool that declined and a tool that broke share one flag only for a
        // ledger that cannot tell a working guard from a failing disk.
        sink.Recorded.Select(u => u.Outcome).Should()
            .Equal(ToolOutcome.Answered, ToolOutcome.Refused, ToolOutcome.Error);
    }

    [Fact]
    public async Task A_call_to_an_unadvertised_tool_is_metered_too()
    {
        var sink = new RecordingSink();
        var catalog = Build(sink, new FakeProvider("known"));

        await catalog.InvokeAsync(new ToolCall("missing", Empty()), TestContext.Current.CancellationToken);

        // This reverses the earlier behaviour deliberately. An unknown call used to be dropped because
        // it "never reaches a provider" — which made a client hammering a tool this server does not
        // advertise (a stale configuration, almost always) the one event nobody could see.
        sink.Recorded.Should().ContainSingle()
            .Which.Should().Match<ToolUsage>(u => u.ToolName == "missing" && u.Outcome == ToolOutcome.Error);
    }

    [Fact]
    public async Task A_record_carries_the_arguments_the_scope_and_the_answer_size()
    {
        var sink = new RecordingSink();
        var catalog = Build(sink, new FakeProvider("known"));

        await catalog.InvokeAsync(
            new ToolCall("known", JsonDocument.Parse("""{"path":"a.txt"}""").RootElement.Clone()),
            TestContext.Current.CancellationToken);

        var usage = sink.Recorded.Should().ContainSingle().Subject;
        usage.ArgumentsJson.Should().Contain("a.txt");
        usage.ArgumentsTruncatedBytes.Should().Be(0);
        usage.Scope.Should().Be("fake-scope");
        usage.ResponseChars.Should().Be("done".Length);
        usage.Error.Should().BeEmpty("an answered call has nothing to explain");
    }

    [Fact]
    public async Task Tokens_are_recorded_as_not_captured_rather_than_as_zero()
    {
        var sink = new RecordingSink();
        var catalog = Build(sink, new FakeProvider("known"));

        await catalog.InvokeAsync(new ToolCall("known", Empty()), TestContext.Current.CancellationToken);

        // A zero that means "none" and a zero that means "nobody counted" are opposite readings of one
        // number, and only one of them is safe to aggregate.
        var tokens = sink.Recorded.Single().Tokens;
        tokens.WasCaptured.Should().BeFalse();
        tokens.Reason.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Without_a_presentation_the_caller_is_not_captured_rather_than_guessed()
    {
        var sink = new RecordingSink();
        var catalog = Build(sink, new FakeProvider("known"));

        await catalog.InvokeAsync(new ToolCall("known", Empty()), TestContext.Current.CancellationToken);

        var caller = sink.Recorded.Single().Caller;
        caller.ClientName.WasCaptured.Should().BeFalse();
        caller.ClientName.Reason.Should().NotBeEmpty("an unknown caller must say why it is unknown");
    }

    [Fact]
    public void The_advertised_list_is_name_ordered_so_the_surface_is_stable()
    {
        var catalog = Build(new FakeProvider("zulu"), new FakeProvider("alpha"));

        catalog.Advertised.Select(t => t.Name).Should().Equal("alpha", "zulu");
    }

    internal static ToolCatalog Build(params IToolProvider[] providers) =>
        Build(new NullUsageSink(), providers);

    internal static ToolCatalog Build(IUsageSink sink, params IToolProvider[] providers) =>
        new(providers, sink, new AmbientCallerContext(),
            new FakeTimeProvider(new DateTimeOffset(2026, 8, 15, 12, 0, 0, TimeSpan.Zero)),
            NullLogger<ToolCatalog>.Instance);

    internal static JsonElement Empty() => JsonDocument.Parse("{}").RootElement.Clone();

    private sealed class FakeProvider(string name) : IToolProvider
    {
        public IReadOnlyList<ToolSchema> Tools { get; } =
        [
            new ToolSchema
            {
                Name = name,
                Description = "fake",
                InputSchema = ToolSchema.ParseSchema("""{"type":"object"}"""),
            },
        ];

        public string Scope => "fake-scope";

        public Task<ToolResult> InvokeAsync(ToolCall call, CancellationToken cancellationToken) =>
            Task.FromResult(ToolResult.Success("done"));
    }

    private sealed class RefusingProvider : IToolProvider
    {
        public IReadOnlyList<ToolSchema> Tools { get; } =
        [
            new ToolSchema
            {
                Name = "refuses",
                Description = "always declines",
                InputSchema = ToolSchema.ParseSchema("""{"type":"object"}"""),
            },
        ];

        public Task<ToolResult> InvokeAsync(ToolCall call, CancellationToken cancellationToken) =>
            Task.FromResult(ToolResult.Refusal("outside the workspace"));
    }

    private sealed class BreakingProvider : IToolProvider
    {
        public IReadOnlyList<ToolSchema> Tools { get; } =
        [
            new ToolSchema
            {
                Name = "breaks",
                Description = "always fails",
                InputSchema = ToolSchema.ParseSchema("""{"type":"object"}"""),
            },
        ];

        public Task<ToolResult> InvokeAsync(ToolCall call, CancellationToken cancellationToken) =>
            Task.FromResult(ToolResult.Failure("the disk went away"));
    }

    private sealed class RecordingSink : IUsageSink
    {
        public List<ToolUsage> Recorded { get; } = [];

        public Task RecordAsync(ToolUsage usage, CancellationToken cancellationToken)
        {
            Recorded.Add(usage);
            return Task.CompletedTask;
        }
    }
}
