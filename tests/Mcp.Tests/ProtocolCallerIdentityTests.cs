using System.IO.Pipelines;
using FluentAssertions;
using Mcp.Application;
using Mcp.Contracts;
using Mcp.Server;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Xunit;

namespace Mcp.Tests;

/// <summary>The identity was always on the wire; this server just never read it. Asserted through a
/// REAL client and a real server over a real protocol session, because the whole claim is about what
/// the transport carries — a unit test with a hand-made context would prove only that my hand-made
/// context has the field I put in it.</summary>
public sealed class ProtocolCallerIdentityTests
{
    [Fact]
    public async Task A_tool_call_carries_the_calling_client_s_name_and_version()
    {
        var sink = new RecordingSink();
        await CallOverTheProtocol(sink, new Implementation { Name = "claude-code", Version = "9.9.9" });

        var caller = sink.Recorded.Should().ContainSingle().Subject.Caller;
        caller.ClientName.Should().Be(Captured.Text("claude-code"));
        caller.ClientVersion.Should().Be(Captured.Text("9.9.9"));
        caller.Transport.Should().Be("test-stream");
    }

    [Fact]
    public async Task The_caller_s_model_is_recorded_as_not_captured_rather_than_guessed()
    {
        var sink = new RecordingSink();
        await CallOverTheProtocol(sink, new Implementation { Name = "claude-code", Version = "9.9.9" });

        // No MCP revision tells a server which model is driving the session. Deriving it from the
        // client name would be the single most tempting and most wrong field in the record: an agent
        // called "claude-code" may be running any model at all.
        var model = sink.Recorded.Single().Caller.Model;
        model.WasCaptured.Should().BeFalse();
        model.Value.Should().BeEmpty();
        model.Reason.Should().Contain("model");
    }

    /// <summary>Runs one real tools/call from a real client against a real server over an in-memory
    /// stream pair, and returns what the usage sink saw.</summary>
    private static async Task CallOverTheProtocol(IUsageSink sink, Implementation client)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var toServer = new Pipe();
        var toClient = new Pipe();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(sink);
        services.AddMcpApplication();
        services.AddSingleton<IToolProvider>(new AnsweringProvider());
        services.AddMcpServer().WithCatalogTools("test-stream");

        await using var provider = services.BuildServiceProvider();
        await using var serverTransport = new StreamServerTransport(
            toServer.Reader.AsStream(), toClient.Writer.AsStream(), "test-server");

        await using var server = McpServer.Create(
            serverTransport,
            provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<McpServerOptions>>().Value,
            NullLoggerFactory.Instance,
            provider);

        var running = server.RunAsync(cancellationToken);

        await using var mcpClient = await McpClient.CreateAsync(
            new StreamClientTransport(toServer.Writer.AsStream(), toClient.Reader.AsStream()),
            new McpClientOptions { ClientInfo = client },
            NullLoggerFactory.Instance,
            cancellationToken);

        await mcpClient.CallToolAsync(
            AnsweringProvider.ToolName,
            new Dictionary<string, object?> { ["path"] = "a.txt" },
            cancellationToken: cancellationToken);

        await mcpClient.DisposeAsync();
        await server.DisposeAsync();
        await running.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
    }

    private sealed class AnsweringProvider : IToolProvider
    {
        public const string ToolName = "rt_read_local_file";

        public IReadOnlyList<ToolSchema> Tools { get; } =
        [
            new ToolSchema
            {
                Name = ToolName,
                Description = "answers whatever it is asked",
                InputSchema = ToolSchema.ParseSchema(
                    """{"type":"object","properties":{"path":{"type":"string"}}}"""),
            },
        ];

        public string Scope => "D:/work/repo";

        public Task<ToolResult> InvokeAsync(ToolCall call, CancellationToken cancellationToken) =>
            Task.FromResult(ToolResult.Success("lines 1-1 of 1\nalpha"));
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
