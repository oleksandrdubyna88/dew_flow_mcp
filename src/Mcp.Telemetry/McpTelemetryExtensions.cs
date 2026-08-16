using Mcp.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Mcp.Telemetry;

/// <summary>Registration for the spool sink. Opt-in by configuration: a host that names no spool
/// directory keeps the null sink, so telemetry is something an operator turns on rather than something
/// that appears on disk because a package was referenced.</summary>
public static class McpTelemetryExtensions
{
    /// <param name="spoolDirectory">Where spool files are written. Blank ⇒ nothing is registered and
    /// the null sink stays in place.</param>
    /// <param name="app">How this host names itself in every line.</param>
    /// <param name="correlation">What unit of work the caller declared this process to be serving.
    /// Defaulted to unattributed, which is the truth about every real session and keeps every existing
    /// call site unchanged.</param>
    public static IServiceCollection AddTelemetrySpool(
        this IServiceCollection services,
        string? spoolDirectory,
        string app,
        TelemetryCorrelation? correlation = null)
    {
        if (string.IsNullOrWhiteSpace(spoolDirectory))
        {
            return services;
        }

        services.TryAddSingleton(TimeProvider.System);

        // Not TryAdd: naming a spool directory IS the instruction to replace the null floor, and a
        // silent no-op here would be a host that looks configured and records nothing.
        services.AddSingleton(new SpoolOptions
        {
            Directory = spoolDirectory,
            App = app,
            Correlation = correlation ?? TelemetryCorrelation.None,
        });
        services.AddSingleton<SpoolUsageSink>(sp => new SpoolUsageSink(
            sp.GetRequiredService<SpoolOptions>(),
            sp.GetRequiredService<TimeProvider>(),
            sp.GetRequiredService<ILogger<SpoolUsageSink>>()));
        services.AddSingleton<IUsageSink>(sp => sp.GetRequiredService<SpoolUsageSink>());

        // The same instance as a health contributor: a spool whose writer has stopped is invisible
        // from outside the process otherwise, and "the server is up" is not the question an
        // orchestrator is asking.
        services.AddSingleton<IHealthContributor>(sp => sp.GetRequiredService<SpoolUsageSink>());

        return services;
    }
}
