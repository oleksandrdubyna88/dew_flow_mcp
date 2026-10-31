using FluentAssertions;
using Mcp.Tests.Leftovers;
using Xunit;

namespace Mcp.Tests;

/// <summary>The token file is copied from repository to repository, so its own row proves nothing: a copy that kept another repository's
/// <c>OwnedToken.cs</c> would register ITS token against ITS row and pass every check made from inside the folder. This repository's identity
/// is read from outside it — the solution file's name — and the token file is checked against that.</summary>
public sealed class RepositoryIdentityTests
{
    [Fact]
    public void The_token_file_carries_this_repositorys_token_and_row()
    {
        RepositoryIdentity.Problems(OwnedToken.RepoToken, OwnedToken.RepoRow, RepositoryIdentity.ActualSolutionName()).Should().BeEmpty();
    }

    [Fact]
    public void The_registered_row_of_this_repositorys_token_names_this_repository_by_the_outside_identity()
    {
        LeftoversGuard.RegisteredTokenFindings(RepositoryRoot.Path, RepositoryIdentity.Token, RepositoryIdentity.Row).Should().BeEmpty();
    }

    [Theory]
    [InlineData("sidecar", "the embedding sidecar", "dew_flow_mcp")]
    [InlineData("mcp", "the embedding sidecar", "dew_flow_mcp")]
    [InlineData("sidecar", "the MCP server", "dew_flow_mcp")]
    [InlineData("mcp", "the MCP server", "dew_flow_sidecar_rust")]
    public void Another_repositorys_token_or_row_or_a_tree_that_is_not_this_repository_is_a_problem(string token, string row, string solution)
    {
        RepositoryIdentity.Problems(token, row, solution).Should().NotBeEmpty();
    }

    [Fact]
    public void This_repositorys_own_pair_is_no_problem()
    {
        RepositoryIdentity.Problems("mcp", "the MCP server", "dew_flow_mcp").Should().BeEmpty();
    }
}
