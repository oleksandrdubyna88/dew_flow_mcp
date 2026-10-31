namespace Mcp.Tests;

/// <summary>Who THIS repository is, written outside the helper folder that is copied between repositories: the name of the solution file at
/// the root, its token and its row of the rule's <c>## Tokens</c> table. A copy of the helper into another repository does not carry this
/// file, and a copy of this file into another repository fails its first check (the solution's name).</summary>
internal static class RepositoryIdentity
{
    public const string SolutionName = "dew_flow_mcp";

    public const string Token = "mcp";

    public const string Row = "the MCP server";

    /// <summary>The name of the one <c>*.slnx</c> at the repository's root, without its extension.</summary>
    public static string ActualSolutionName() => Path.GetFileNameWithoutExtension(Directory.GetFiles(RepositoryRoot.Path, "*.slnx").Single());

    /// <summary>Why <paramref name="token"/> and <paramref name="row"/> (from the token file) in a tree whose solution file is named
    /// <paramref name="solution"/> are not this repository's — empty when they are.</summary>
    public static IReadOnlyList<string> Problems(string token, string row, string solution) =>
        new[]
        {
            solution == SolutionName ? null : $"the solution file is `{solution}`, this identity is `{SolutionName}`'s",
            token == Token ? null : $"RepoToken is `{token}`, this repository's is `{Token}`",
            row == Row ? null : $"RepoRow is `{row}`, this repository's is `{Row}`",
        }.OfType<string>().ToList();
}
