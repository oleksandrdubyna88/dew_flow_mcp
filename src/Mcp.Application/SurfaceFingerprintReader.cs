using System.Reflection;
using Mcp.Contracts;

namespace Mcp.Application;

/// <summary>Which process is answering. One machine runs several of these hosts, and a fingerprint that
/// cannot say which one wrote it is a fingerprint two deployments share.</summary>
public sealed record SurfaceIdentity(string App);

/// <summary>Answers "what is this server actually advertising" from the catalog itself.
/// <para>It reads <see cref="ToolCatalog.Advertised"/>, never the description files: the files are what
/// the surface was ASKED to serve, and the whole point of an echo is that those two can differ.</para>
/// </summary>
public sealed class SurfaceFingerprintReader(
    ToolCatalog catalog,
    ToolSurfaceOptions surface,
    SurfaceIdentity identity,
    TimeProvider clock)
{
    public SurfaceFingerprint Read() =>
        SurfaceFingerprint.Of(
            catalog.Advertised,
            surface.DescriptionSet,
            identity.App,
            Environment.ProcessId,
            HostVersion,
            clock.GetUtcNow());

    /// <summary>What the entry assembly says about itself, or an explicit "not captured" with the
    /// reason.
    /// <para>The plan asked for a <c>BuiltAt</c> timestamp here and it is deliberately not that: .NET's
    /// deterministic builds replace the PE header's link timestamp with a content hash, so a "build
    /// time" would be either unobtainable or invented — and rendering a value the thing itself did not
    /// answer is the exact failure the <see cref="Captured"/> shape exists to prevent. The informational
    /// version is what the assembly genuinely reports; the two hashes identify the surface itself.
    /// </para></summary>
    private static Captured HostVersion
    {
        get
        {
            var entry = Assembly.GetEntryAssembly();
            if (entry is null)
            {
                return Captured.Unavailable("the process has no managed entry assembly to ask");
            }

            var informational = entry
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

            return string.IsNullOrWhiteSpace(informational)
                ? Captured.Unavailable($"'{entry.GetName().Name}' declares no informational version")
                : Captured.Text(informational);
        }
    }
}
