using System.ComponentModel;
using ModelContextProtocol.Server;

namespace AzDevOps.McpServer;

public partial class Tools
{
    [McpServerTool(
        Title = "My Work Items",
        ReadOnly = true,
        Idempotent = true,
        Destructive = false),
        Description("Returns work items assigned to the current Azure DevOps user using a safe WIQL pattern.")]
    public Task<OperationResult> GetMyWorkItems(
        [Description("Optional: Filter by state, e.g. 'Active' or 'Em Desenvolvimento'")] string? state = null,
        [Description("Optional: Max number of items to return from the WIQL result set before paging details (default: 50, max: 200)")] int? top = WorkItemQueryDefaults.DefaultTop,
        [Description("Optional: Comma-separated list of fields to return (default: compact summary fields)")] string? fieldsCsv = null,
        [Description("Optional: Include relations (default: false)")] bool includeRelations = false,
        [Description("Optional: Skip N items from the matched WIQL result before returning the current page")] int skip = 0,
        [Description("Optional: Page size of returned work items (default: 50, max: 200)")] int pageSize = WorkItemQueryDefaults.DefaultPageSize,
        [Description("Project name or id (optional, uses default if configured)")] string? project = null)
    {
        var wiql = WorkItemConvenienceQueryBuilder.BuildAssignedToMeWiql(state: state);
        return QueryWorkItems(wiql, top, fieldsCsv, includeRelations, skip, pageSize, project);
    }

    [McpServerTool(
        Title = "My User Stories",
        ReadOnly = true,
        Idempotent = true,
        Destructive = false),
        Description("Returns user stories assigned to the current Azure DevOps user using safe @Project and @Me filters.")]
    public Task<OperationResult> GetMyUserStories(
        [Description("Optional: Filter by state, e.g. 'Active' or 'Em Desenvolvimento'")] string? state = null,
        [Description("Optional: Max number of items to return from the WIQL result set before paging details (default: 50, max: 200)")] int? top = WorkItemQueryDefaults.DefaultTop,
        [Description("Optional: Comma-separated list of fields to return (default: compact summary fields)")] string? fieldsCsv = null,
        [Description("Optional: Include relations (default: false)")] bool includeRelations = false,
        [Description("Optional: Skip N items from the matched WIQL result before returning the current page")] int skip = 0,
        [Description("Optional: Page size of returned work items (default: 50, max: 200)")] int pageSize = WorkItemQueryDefaults.DefaultPageSize,
        [Description("Project name or id (optional, uses default if configured)")] string? project = null)
    {
        var wiql = WorkItemConvenienceQueryBuilder.BuildAssignedToMeWiql(workItemType: "User Story", state: state);
        return QueryWorkItems(wiql, top, fieldsCsv, includeRelations, skip, pageSize, project);
    }
}

internal static class WorkItemConvenienceQueryBuilder
{
    public static string BuildAssignedToMeWiql(string? workItemType = null, string? state = null)
    {
        var conditions = new List<string>
        {
            "[System.TeamProject] = @Project",
            "[System.AssignedTo] = @Me"
        };

        if (!string.IsNullOrWhiteSpace(workItemType))
        {
            conditions.Add($"[System.WorkItemType] = '{EscapeWiqlLiteral(workItemType)}'");
        }

        if (!string.IsNullOrWhiteSpace(state))
        {
            conditions.Add($"[System.State] = '{EscapeWiqlLiteral(state)}'");
        }

        return $"SELECT [System.Id] FROM WorkItems WHERE {string.Join(" AND ", conditions)} ORDER BY [System.ChangedDate] DESC";
    }

    private static string EscapeWiqlLiteral(string value) => value.Replace("'", "''", StringComparison.Ordinal);
}
