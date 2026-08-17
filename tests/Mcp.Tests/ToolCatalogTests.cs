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
    public async Task A_provider_that_throws_is_answered_as_a_failure_and_still_reaches_the_usage_sink()
    {
        var sink = new RecordingSink();
        var catalog = Build(sink, new ThrowingProvider());

        var result = await catalog.InvokeAsync(new ToolCall("throws", Empty()), TestContext.Current.CancellationToken);

        // The provider contract permits a throw for genuine infrastructure faults, and the sandboxed
        // reader takes it (a locked file, a delete between the exists-check and the read). Without a
        // guard here the exception leaves the catalog, the session goes with it — and RecordAsync
        // never runs, so the ONE call worth investigating is the only one telemetry never sees.
        result.Should().BeOfType<ToolResult.Failed>()
            .Which.Message.Should().Contain("throws");
        sink.Recorded.Should().ContainSingle()
            .Which.Outcome.Should().Be(ToolOutcome.Error);
    }

    [Fact]
    public async Task A_tool_that_never_returns_is_cut_off_at_the_servers_own_ceiling()
    {
        var sink = new RecordingSink();
        var catalog = Build(sink, new ToolCatalogOptions { CallTimeout = TimeSpan.FromMilliseconds(200) }, new HangingProvider());

        var call = catalog.InvokeAsync(new ToolCall("hangs", Empty()), TestContext.Current.CancellationToken);
        var first = await Task.WhenAny(call, Task.Delay(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));

        // On stdio the caller's token is the only bound a provider gets, and a client that wedges or
        // walks away never cancels it — so without a ceiling of its own the server holds the call for
        // the life of the process.
        first.Should().Be(call, "the server must not wait on a wedged tool forever");
        (await call).Should().BeOfType<ToolResult.Failed>()
            .Which.Message.Should().Contain("ceiling");
        sink.Recorded.Should().ContainSingle()
            .Which.Outcome.Should().Be(ToolOutcome.Error, "a call the server gave up on is a fact about the server");
    }

    /// <summary>
    /// The ceiling holds against a provider that does not cooperate — which is the only case it was ever
    /// needed for.
    ///
    /// <para>The test above passes with a provider that OBSERVES its token, so it proves the token is
    /// passed and nothing more. A provider that ignores it is not a hypothetical: this catalog's whole
    /// design is that providers are implemented in other repositories, and the ordinary way to ignore a
    /// token is a blocking call in a library that never took one. For that provider the ceiling was a
    /// promise the server could not keep — the await simply did not return, and the caller got no answer
    /// at all rather than a late one.</para>
    /// </summary>
    [Fact]
    public async Task A_tool_that_ignores_its_cancellation_token_is_still_answered_at_the_ceiling()
    {
        var sink = new RecordingSink();
        var catalog = Build(sink, new ToolCatalogOptions { CallTimeout = TimeSpan.FromMilliseconds(200) }, new DeafProvider());

        var call = catalog.InvokeAsync(new ToolCall("deaf", Empty()), TestContext.Current.CancellationToken);
        var first = await Task.WhenAny(call, Task.Delay(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));

        first.Should().Be(call,
            "a ceiling that only binds providers which agree to be bound is not a ceiling");
        (await call).Should().BeOfType<ToolResult.Failed>()
            .Which.Message.Should().Contain("ceiling");
        catalog.Abandoned.Should().Be(1,
            "the work is still running behind that answer, and a leak nobody counts is a leak nobody finds");
    }

    [Fact]
    public void A_ceiling_that_cannot_be_applied_stops_the_host_instead_of_throwing_on_every_call()
    {
        var build = () => Build(new NullUsageSink(), new ToolCatalogOptions { CallTimeout = TimeSpan.FromSeconds(-30) }, new FakeProvider("known"));

        // CancelAfter would throw per CALL for the life of the process; a configuration typo belongs
        // in one startup message instead.
        build.Should().Throw<InvalidOperationException>()
            .WithMessage("*CallTimeout*");
    }

    [Fact]
    public async Task A_sink_that_throws_loses_the_record_and_not_the_call()
    {
        var catalog = Build(new BreakingSink(), new FakeProvider("known"));

        var result = await catalog.InvokeAsync(new ToolCall("known", Empty()), TestContext.Current.CancellationToken);

        // Telemetry may never fail a tool call — including the guarded path, where a throwing sink
        // would otherwise lose the very session the provider guard just saved.
        result.Should().BeOfType<ToolResult.Ok>();
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
        Build(sink, new ToolCatalogOptions(), providers);

    internal static ToolCatalog Build(IUsageSink sink, ToolCatalogOptions options, params IToolProvider[] providers) =>
        new(providers, options, sink, new AmbientCallerContext(),
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

    /// <summary>The shape <see cref="IToolProvider"/> explicitly allows: an unexpected infrastructure
    /// fault, thrown rather than returned. The sandboxed reader does exactly this when a file is
    /// deleted between the exists-check and the read.</summary>
    private sealed class ThrowingProvider : IToolProvider
    {
        public IReadOnlyList<ToolSchema> Tools { get; } =
        [
            new ToolSchema
            {
                Name = "throws",
                Description = "an infrastructure fault, not a tool failure",
                InputSchema = ToolSchema.ParseSchema("""{"type":"object"}"""),
            },
        ];

        public Task<ToolResult> InvokeAsync(ToolCall call, CancellationToken cancellationToken) =>
            throw new IOException("the file was locked by another process");
    }

    /// <summary>A provider that honours the token and otherwise never finishes — a wedged retrieval
    /// call, or a lock nobody releases.</summary>
    private sealed class HangingProvider : IToolProvider
    {
        public IReadOnlyList<ToolSchema> Tools { get; } =
        [
            new ToolSchema
            {
                Name = "hangs",
                Description = "never returns on its own",
                InputSchema = ToolSchema.ParseSchema("""{"type":"object"}"""),
            },
        ];

        public async Task<ToolResult> InvokeAsync(ToolCall call, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return ToolResult.Success("unreachable");
        }
    }

    /// <summary>A provider that takes the token and does not look at it — the shape of a third-party
    /// implementation wrapping a blocking library call.</summary>
    private sealed class DeafProvider : IToolProvider
    {
        public IReadOnlyList<ToolSchema> Tools { get; } =
        [
            new ToolSchema
            {
                Name = "deaf",
                Description = "ignores the token it is handed",
                InputSchema = ToolSchema.ParseSchema("""{"type":"object"}"""),
            },
        ];

        public async Task<ToolResult> InvokeAsync(ToolCall call, CancellationToken cancellationToken)
        {
            await Task.Delay(TimeSpan.FromSeconds(30), CancellationToken.None);
            return ToolResult.Success("far too late");
        }
    }

    private sealed class BreakingSink : IUsageSink
    {
        public Task RecordAsync(ToolUsage usage, CancellationToken cancellationToken) =>
            throw new IOException("the spool volume went away");
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
