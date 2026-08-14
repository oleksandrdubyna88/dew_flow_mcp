using System.Text.Json;
using FluentAssertions;
using Mcp.Application;
using Mcp.Contracts;
using Microsoft.Extensions.Logging.Abstractions;
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
    public async Task Every_call_reaches_the_usage_sink_with_its_outcome()
    {
        var sink = new RecordingSink();
        var catalog = Build(sink, new FakeProvider("known"));

        await catalog.InvokeAsync(new ToolCall("known", Empty()), TestContext.Current.CancellationToken);
        await catalog.InvokeAsync(new ToolCall("missing", Empty()), TestContext.Current.CancellationToken);

        // The unknown call never reaches a provider, so only the served one is metered.
        sink.Recorded.Should().ContainSingle()
            .Which.Should().Match<ToolUsage>(u => u.ToolName == "known" && !u.Failed);
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
        new(providers, sink, NullLogger<ToolCatalog>.Instance);

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

        public Task<ToolResult> InvokeAsync(ToolCall call, CancellationToken cancellationToken) =>
            Task.FromResult(ToolResult.Success("done"));
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
