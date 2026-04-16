using AzDevOps.McpServer;

namespace AzDevOpsMcp.Tests;

public class WorkItemConvenienceQueryBuilderTests
{
    [Fact]
    public void BuildAssignedToMeWiql_UsesProjectAndMeMacros()
    {
        var wiql = WorkItemConvenienceQueryBuilder.BuildAssignedToMeWiql();

        Assert.Contains("[System.TeamProject] = @Project", wiql);
        Assert.Contains("[System.AssignedTo] = @Me", wiql);
        Assert.Contains("ORDER BY [System.ChangedDate] DESC", wiql);
    }

    [Fact]
    public void BuildAssignedToMeWiql_EscapesFilterValues()
    {
        var wiql = WorkItemConvenienceQueryBuilder.BuildAssignedToMeWiql("User Story", "Bob's State");

        Assert.Contains("[System.WorkItemType] = 'User Story'", wiql);
        Assert.Contains("[System.State] = 'Bob''s State'", wiql);
    }
}
