using System.Net;
using System.Text;
using FluentAssertions;
using Mcp.Ui.Services;
using Xunit;

namespace Mcp.Tests;

/// <summary>The console's read side, and the one distinction it exists to keep: <b>"the server said
/// nothing" and "the server said empty" are opposite facts.</b>
/// <para>A page that renders both as a blank table sends its reader hunting in the wrong place — the
/// same failure the family's <c>Captured</c> shape prevents on the wire, here on the client.</para>
/// </summary>
public sealed class McpConsoleApiTests
{
    private const string OneTool = """
        {"tools":[{"name":"rt_read_local_file","description":"Read a window of a file.","schemaHash":"abc123"}],
         "descriptionSet":"concise-v1","toolsHash":"aa","descriptionsHash":"bb",
         "app":"mcp","pid":7,"version":{"wasCaptured":true,"value":"1.0.0","reason":""},
         "takenAt":"2026-08-16T12:00:00+00:00"}
        """;

    [Fact]
    public async Task A_surface_the_daemon_answered_arrives_with_the_text_it_serves()
    {
        var api = Api(Responds(HttpStatusCode.OK, OneTool));

        var read = await api.GetSurfaceAsync(TestContext.Current.CancellationToken);

        read.Available.Should().BeTrue();
        read.Detail.Should().BeEmpty();
        read.Value!.Tools.Should().ContainSingle()
            .Which.Description.Should().Be("Read a window of a file.");
        read.Value.DescriptionSet.Should().Be("concise-v1");
    }

    [Fact]
    public async Task A_server_advertising_no_tools_is_a_successful_read_of_an_empty_surface()
    {
        var api = Api(Responds(HttpStatusCode.OK, """
            {"tools":[],"descriptionSet":"","toolsHash":"aa","descriptionsHash":"bb","app":"mcp","pid":7,
             "version":{"wasCaptured":false,"value":"","reason":"no informational version"},
             "takenAt":"2026-08-16T12:00:00+00:00"}
            """));

        var read = await api.GetSurfaceAsync(TestContext.Current.CancellationToken);

        // Available with nothing in it — a subset that excluded everything, or no provider registered.
        // The page must be able to say THAT rather than "could not read".
        read.Available.Should().BeTrue();
        read.Value!.Tools.Should().BeEmpty();
    }

    [Fact]
    public async Task A_daemon_that_cannot_be_reached_is_an_unread_surface_with_a_reason()
    {
        var api = Api(Throws(new HttpRequestException("connection refused")));

        var read = await api.GetSurfaceAsync(TestContext.Current.CancellationToken);

        // NOT an empty surface. A console that throws here shows a blank screen and puts the only
        // explanation somewhere the reader cannot see.
        read.Available.Should().BeFalse();
        read.Value.Should().BeNull();
        read.Detail.Should().Contain("could not be reached").And.Contain("connection refused");
    }

    [Fact]
    public async Task A_body_the_console_cannot_parse_says_so_instead_of_looking_empty()
    {
        var api = Api(Responds(HttpStatusCode.OK, "{not json"));

        var read = await api.GetSurfaceAsync(TestContext.Current.CancellationToken);

        read.Available.Should().BeFalse();
        read.Detail.Should().Contain("could not read");
    }

    [Fact]
    public async Task A_request_that_never_answers_is_reported_as_a_timeout_not_as_nothing_there()
    {
        var api = Api(Throws(new TaskCanceledException("timed out")));

        var read = await api.GetHealthAsync(TestContext.Current.CancellationToken);

        // "Slow" and "broken" are both different from "there is nothing there", and an operator
        // deciding whether to restart something needs to tell them apart.
        read.Available.Should().BeFalse();
        read.Detail.Should().Contain("did not answer in time");
    }

    [Fact]
    public void Nothing_asked_yet_is_its_own_state()
    {
        // Distinct from a failed read: before the first request there is no reason to show, and a page
        // that prints one invents a failure that has not happened.
        Read<string>.Unasked.Available.Should().BeFalse();
        Read<string>.Unasked.Detail.Should().BeEmpty();
    }

    private static McpConsoleApi Api(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://daemon.test/") });

    private static HttpMessageHandler Responds(HttpStatusCode status, string body) =>
        new StubHandler(_ => new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        });

    private static HttpMessageHandler Throws(Exception fault) =>
        new StubHandler(_ => throw fault);

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(answer(request));
    }
}
