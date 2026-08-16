using Mcp.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Workspace.Application;

namespace Workspace.Infrastructure;

/// <summary>Registration for the workspace provider. Note the shape: the module adds ITSELF to the
/// container as an <see cref="IToolProvider"/>. The MCP module never names it — the host decides who
/// is in the room, and that is the whole inversion in one line.</summary>
public static class WorkspaceExtensions
{
    public static IServiceCollection AddWorkspaceTools(this IServiceCollection services, string rootPath)
    {
        services.AddSingleton(new WorkspaceRoot(rootPath));

        // TryAdd: a host whose workspace holds legitimately huge files registers its own caps BEFORE
        // this and keeps them. What a host that says nothing gets is the default ceiling — never none.
        services.TryAddSingleton(new SandboxedFileReaderOptions());
        services.AddSingleton<ISandboxedFileReader, SandboxedFileReader>();
        services.AddSingleton<IToolProvider, WorkspaceToolProvider>();
        return services;
    }
}
