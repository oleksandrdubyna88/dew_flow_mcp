using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Mcp.Contracts;

/// <summary>What a running server is ACTUALLY advertising — every tool name, the exact description
/// text it serves, and hashes a caller can compare.
/// <para>This is the family's <b>declare and echo, never assume</b> discipline applied to the tool
/// surface: what a configuration asked for and what a process is serving are two different facts, and
/// only the second one explains a result. Reading it currently means reading the binary.</para>
/// <para><b>The hashes are computed here and quoted, never re-derived by a consumer.</b> A second
/// implementation of a canonicalisation is two implementations that must agree byte for byte forever;
/// a consumer stores and compares the string this server printed.</para></summary>
public sealed record SurfaceFingerprint(
    IReadOnlyList<ToolDescriptionEcho> Tools,
    string DescriptionSet,
    string ToolsHash,
    string DescriptionsHash,
    string App,
    int Pid,
    Captured Version,
    DateTimeOffset TakenAt)
{
    /// <summary>Builds the fingerprint from what the catalog advertises — never from the description
    /// files, which are what the surface was ASKED to serve rather than what it serves.</summary>
    public static SurfaceFingerprint Of(
        IReadOnlyList<ToolSchema> advertised,
        string descriptionSet,
        string app,
        int pid,
        Captured version,
        DateTimeOffset takenAt)
    {
        // Sorted explicitly. The catalog already orders by name, but a hash whose stability depends on
        // somebody else's ordering is a hash that changes the day that ordering does.
        var ordered = advertised.OrderBy(tool => tool.Name, StringComparer.Ordinal).ToList();

        return new SurfaceFingerprint(
            [.. ordered.Select(tool => new ToolDescriptionEcho(
                tool.Name, tool.Description, Hash(tool.InputSchema.GetRawText())))],
            descriptionSet,
            Hash(string.Join("\n", ordered.Select(tool => tool.Name))),
            Hash(string.Join("\n", ordered.Select(tool => $"{tool.Name}={tool.Description}"))),
            app,
            pid,
            version,
            takenAt);
    }

    private static string Hash(string text) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}

/// <summary>One advertised tool as the surface presents it. The description is the TEXT, in full,
/// because a hash alone answers "did it change" and never "to what".</summary>
public sealed record ToolDescriptionEcho(string Name, string Description, string SchemaHash);

/// <summary>The one serializer for the fingerprint, so `--print-surface` and the HTTP endpoint emit the
/// same shape and a test can assert the bytes.</summary>
public static class SurfaceJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        // Indented: unlike a telemetry line this is read by a person as often as by a script, and it is
        // emitted once per process rather than once per call.
        WriteIndented = true,
    };

    public static string Text(SurfaceFingerprint fingerprint) =>
        JsonSerializer.Serialize(fingerprint, Options);
}
