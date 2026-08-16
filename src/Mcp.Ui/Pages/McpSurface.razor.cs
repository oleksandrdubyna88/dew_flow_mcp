using Mcp.Contracts;
using Mcp.Ui.Services;
using Microsoft.AspNetCore.Components;

namespace Mcp.Ui.Pages;

/// <summary>
/// What this MCP server is actually advertising: every tool, the exact description text it serves, the
/// hashes, and the health of the components behind it.
///
/// <para><b>Declare and echo, never assume.</b> What a configuration asked for and what a process is
/// serving are two different facts, and only the second one explains a result. Before this page the
/// second could only be had by reading the binary, or by curling an endpoint and reading JSON.</para>
///
/// <para>The description is rendered in FULL rather than as its hash. A hash answers "did it change"
/// and never "to what", and the wording is the thing an agent routes on — measured on the previous
/// generation, rewriting one instruction about which tool to use when moved a score 16.5 points of 63,
/// while quadrupling the toolbox moved it 1.</para>
/// </summary>
public partial class McpSurface(McpConsoleApi api) : ComponentBase
{
    private Read<SurfaceFingerprint> Surface { get; set; } = Read<SurfaceFingerprint>.Unasked;

    private Read<McpApiHealth> Health { get; set; } = Read<McpApiHealth>.Unasked;

    private bool Loading { get; set; }

    /// <summary>Why the surface could not be read, or empty before anything was asked. The two are
    /// different states and the page says so rather than showing one message for both.</summary>
    private string Detail => Surface.Detail;

    protected override async Task OnInitializedAsync() => await ReloadAsync();

    /// <summary>
    /// Both reads, independently.
    ///
    /// <para>Not <c>Task.WhenAll</c> over a shared failure: a daemon whose health probe is broken can
    /// still describe its surface perfectly well, and folding the two into one outcome would hide the
    /// half that worked. Each panel above renders its own answer, including its own reason for not
    /// having one.</para>
    /// </summary>
    private async Task ReloadAsync()
    {
        Loading = true;
        Surface = await api.GetSurfaceAsync();
        Health = await api.GetHealthAsync();
        Loading = false;
    }
}
