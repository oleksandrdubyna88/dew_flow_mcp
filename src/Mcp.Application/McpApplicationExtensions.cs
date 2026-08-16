using System.Collections.Frozen;
using Mcp.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Mcp.Application;

/// <summary>Registration for the tool-hosting core. Providers register themselves separately — this
/// module never names one.</summary>
public static class McpApplicationExtensions
{
    /// <summary>The shipped surface: every registered tool, every compiled description.</summary>
    public static IServiceCollection AddMcpApplication(this IServiceCollection services) =>
        services.AddMcpApplication(ToolSurfaceOptions.Everything);

    /// <summary>The same core, serving the tools and the descriptions this process was configured for.
    /// <para>The configuration is applied by wrapping each registered provider BEFORE the catalog is
    /// built, never inside it — see <see cref="ToolSurfaceProvider"/> for why that placement is the
    /// design. A surface that configures nothing takes the untouched path, so the default is not merely
    /// equivalent to today's behaviour but is literally the same code.</para></summary>
    public static IServiceCollection AddMcpApplication(this IServiceCollection services, ToolSurfaceOptions surface)
    {
        // TryAdd so a host that ships a real telemetry sink keeps it; the null sink is only a floor.
        services.TryAddSingleton<IUsageSink, NullUsageSink>();
        services.TryAddSingleton<AmbientCallerContext>();
        services.TryAddSingleton<ICallerContext>(sp => sp.GetRequiredService<AmbientCallerContext>());
        services.TryAddSingleton(TimeProvider.System);

        // TryAdd again: a host whose providers have legitimately slow calls registers its own
        // ToolCatalogOptions BEFORE this and keeps it. The default ceiling is what a host that says
        // nothing gets — never no ceiling at all.
        services.TryAddSingleton(new ToolCatalogOptions());

        GuardAgainstASetWithNowhereToReadItFrom(surface);
        RegisterCatalog(services, surface);
        return services;
    }

    private static void RegisterCatalog(IServiceCollection services, ToolSurfaceOptions surface)
    {
        if (surface.IsEverything)
        {
            services.TryAddSingleton<ToolCatalog>();
            return;
        }

        services.TryAddSingleton(sp => ConfiguredCatalog(sp, surface));
    }

    /// <summary>Builds the catalog over the wrapped providers. The description files are read here,
    /// once, at the first resolution — which for every host this repository ships is startup.</summary>
    private static ToolCatalog ConfiguredCatalog(IServiceProvider services, ToolSurfaceOptions surface)
    {
        var descriptions = ToolDescriptionCatalog.Load(surface.DescriptionsDirectory, surface.DescriptionSet);
        var registered = services.GetServices<IToolProvider>().ToList();
        var providers = registered
            .Select(provider => (IToolProvider)new ToolSurfaceProvider(provider, surface.Tools, descriptions))
            .ToList();

        GuardAgainstAConfigurationThatDoesNotFit(surface, descriptions, registered, providers);
        ReportIgnoredDescriptionFiles(services, descriptions);

        return new ToolCatalog(
            providers,
            services.GetRequiredService<ToolCatalogOptions>(),
            services.GetRequiredService<IUsageSink>(),
            services.GetRequiredService<ICallerContext>(),
            services.GetRequiredService<TimeProvider>(),
            services.GetRequiredService<ILogger<ToolCatalog>>());
    }

    /// <summary>A set named with no directory to read it from would serve every compiled default while
    /// looking configured — caught here rather than at the first puzzled A/B result.</summary>
    private static void GuardAgainstASetWithNowhereToReadItFrom(ToolSurfaceOptions surface)
    {
        if (surface.DescriptionSet.Length == 0 || surface.DescriptionsDirectory.Length > 0)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Description set '{surface.DescriptionSet}' was named without a descriptions directory to read it from.");
    }

    /// <summary>A subset naming a tool nobody offers, or a description file for a tool this surface does
    /// not serve, stops the host naming BOTH sides.
    /// <para>A surface silently smaller than the one somebody configured is the failure this whole seam
    /// exists to make visible; it must not be introducible by a typo.</para>
    /// <para>The two checks measure against DIFFERENT sets, and conflating them was a real defect the
    /// suite caught: a typo in the subset must be answered with every tool a provider OFFERS, because
    /// the filtered list is missing exactly the tools the operator was choosing between. A description
    /// file, by contrast, is answered with what this configuration actually SERVES — a file for a tool
    /// the subset excluded is a mismatch precisely against the narrowed surface.</para></summary>
    private static void GuardAgainstAConfigurationThatDoesNotFit(
        ToolSurfaceOptions surface,
        ToolDescriptionCatalog descriptions,
        IReadOnlyList<IToolProvider> registered,
        IReadOnlyList<IToolProvider> configured)
    {
        var offered = ToolNames(registered);
        var unknown = Missing(surface.Tools, offered);
        if (unknown.Length > 0)
        {
            throw new InvalidOperationException(
                $"The tool subset names {unknown}, which no registered provider offers. "
                + $"{Available("offers", offered)}");
        }

        var served = ToolNames(configured);
        var unmatched = Missing(descriptions.NamedTools, served);
        if (unmatched.Length > 0)
        {
            throw new InvalidOperationException(
                $"The {descriptions.Label} has files for {unmatched}, which this surface does not serve. "
                + $"{Available("serves", served)}");
        }
    }

    private static FrozenSet<string> ToolNames(IReadOnlyList<IToolProvider> providers) =>
        providers
            .SelectMany(provider => provider.Tools)
            .Select(tool => tool.Name)
            .ToFrozenSet(StringComparer.Ordinal);

    private static string Missing(IReadOnlySet<string> wanted, IReadOnlySet<string> available) =>
        string.Join(", ", wanted.Except(available, StringComparer.Ordinal).Order(StringComparer.Ordinal));

    private static string Available(string verb, IReadOnlySet<string> names) =>
        names.Count > 0
            ? $"This server {verb}: {string.Join(", ", names.Order(StringComparer.Ordinal))}."
            : $"This server {verb} no tools at all.";

    /// <summary>A description file that fell back to the compiled default is said out loud. It is a
    /// legitimate fallback and never fails the host — but an override somebody authored and this server
    /// ignored, reported nowhere, is the same invisible failure the guards above exist to prevent.
    /// </summary>
    private static void ReportIgnoredDescriptionFiles(IServiceProvider services, ToolDescriptionCatalog descriptions)
    {
        if (descriptions.Ignored.Count == 0)
        {
            return;
        }

        services.GetRequiredService<ILogger<ToolDescriptionCatalog>>().LogWarning(
            "Description files fell back to the compiled default: {IgnoredDescriptionFiles}",
            string.Join("; ", descriptions.Ignored));
    }
}
