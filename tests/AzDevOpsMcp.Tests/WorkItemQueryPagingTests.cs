using AzDevOps.McpServer;

namespace AzDevOpsMcp.Tests;

public class WorkItemQueryPagingTests
{
    [Fact]
    public void Normalize_DefaultsInvalidInputs_AndAddsWarnings()
    {
        var state = WorkItemQueryPaging.Normalize(top: 0, skip: -5, pageSize: 0);

        Assert.Equal(WorkItemQueryDefaults.DefaultTop, state.Top);
        Assert.Equal(0, state.Skip);
        Assert.Equal(WorkItemQueryDefaults.DefaultPageSize, state.PageSize);
        Assert.Contains(state.Warnings, warning => warning.Code == "top_defaulted");
        Assert.Contains(state.Warnings, warning => warning.Code == "skip_normalized");
        Assert.Contains(state.Warnings, warning => warning.Code == "page_size_defaulted");
    }

    [Fact]
    public void Normalize_CapsOversizedInputs()
    {
        var state = WorkItemQueryPaging.Normalize(top: 999, skip: 3, pageSize: 999);

        Assert.Equal(WorkItemQueryDefaults.MaxTop, state.Top);
        Assert.Equal(3, state.Skip);
        Assert.Equal(WorkItemQueryDefaults.MaxPageSize, state.PageSize);
        Assert.Contains(state.Warnings, warning => warning.Code == "top_capped");
        Assert.Contains(state.Warnings, warning => warning.Code == "page_size_capped");
    }

    [Theory]
    [InlineData("", "WIQL query is required.")]
    [InlineData(" ", "WIQL query is required.")]
    public void ValidateWiql_RejectsMissingWiql(string wiql, string expectedError)
    {
        var error = WorkItemQueryPaging.ValidateWiql(wiql);

        Assert.Equal(expectedError, error);
    }

    [Fact]
    public void ValidateWiql_RejectsWorkItemLinksWithOrderBy()
    {
        const string wiql = "SELECT [System.Id] FROM WorkItemLinks WHERE [System.TeamProject] = @Project ORDER BY [System.ChangedDate] DESC";

        var error = WorkItemQueryPaging.ValidateWiql(wiql);

        Assert.Equal("WIQL queries that use WorkItemLinks with ORDER BY are not supported by this tool.", error);
    }

    [Fact]
    public void GetNextSkip_ReturnsNullWhenPastEnd()
    {
        var state = WorkItemQueryPaging.Normalize(top: 50, skip: 50, pageSize: 25);

        Assert.Null(state.GetNextSkip(50));
    }
}
