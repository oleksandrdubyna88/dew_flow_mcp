using System.Net.Http.Json;
using Mcp.Contracts;

namespace Mcp.Ui.Services;

/// <summary>The console's read side of the MCP management API.
/// <para><b>Every method distinguishes "the server said nothing" from "the server said empty."</b> A
/// surface that could not be read and a server advertising no tools are opposite facts, and a page that
/// renders both as an empty table sends its reader hunting in the wrong place. So each answer is a
/// <see cref="Read{T}"/> carrying the value, whether it arrived, and why it did not.</para></summary>
public sealed class McpConsoleApi(HttpClient http)
{
    /// <summary>What this server is actually advertising, hashes included.</summary>
    public Task<Read<SurfaceFingerprint>> GetSurfaceAsync(CancellationToken cancellationToken = default) =>
        GetAsync<SurfaceFingerprint>("api/mcp/surface", cancellationToken);

    /// <summary>The computed verdict and the components it came from.</summary>
    public Task<Read<McpApiHealth>> GetHealthAsync(CancellationToken cancellationToken = default) =>
        GetAsync<McpApiHealth>("api/mcp/health", cancellationToken);

    /// <summary>One request, with every failure turned into a value the page can render.
    /// <para>A console that throws on an unreachable daemon shows the reader a blank screen and puts the
    /// only explanation in a place they cannot see. The reason travels with the answer instead.</para>
    /// </summary>
    private async Task<Read<T>> GetAsync<T>(string route, CancellationToken cancellationToken)
        where T : class
    {
        try
        {
            var value = await http.GetFromJsonAsync<T>(route, cancellationToken);
            return value is null
                ? Read<T>.Missing($"{route} answered with an empty body")
                : Read<T>.Arrived(value);
        }
        catch (HttpRequestException ex)
        {
            return Read<T>.Missing($"{route} could not be reached: {ex.Message}");
        }
        catch (System.Text.Json.JsonException ex)
        {
            return Read<T>.Missing($"{route} answered with something this console could not read: {ex.Message}");
        }
        catch (TaskCanceledException)
        {
            // Includes the timeout, which HttpClient reports as a cancellation. A reader must be able to
            // tell "slow" from "broken", and neither is "there is nothing there".
            return Read<T>.Missing($"{route} did not answer in time");
        }
    }
}

/// <summary>A value that may not have arrived, and the reason when it did not. The family's
/// flag-and-reason shape, on the console's side of the wire.</summary>
public sealed record Read<T>(bool Available, T? Value, string Detail)
    where T : class
{
    public static Read<T> Arrived(T value) => new(true, value, string.Empty);

    public static Read<T> Missing(string detail) => new(false, null, detail);

    /// <summary>Nothing has been asked yet — deliberately not the same state as a failed read, and
    /// deliberately not "available with nothing in it".</summary>
    public static Read<T> Unasked { get; } = new(false, null, string.Empty);
}
