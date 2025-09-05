using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using ModelContextProtocol.Server;

namespace AzDevOps.McpServer;

public partial class Tools
{
    [McpServerTool(
        Title = "WIQL Query",
        ReadOnly = true,
        Idempotent = true,
        Destructive = false),
        Description("Executes a WIQL query and returns matching work item IDs and basic fields.")]
    public async Task<AzdoOperationResult> QueryWorkItems(
        [Description("WIQL query string")] string wiql,
        [Description("Optional: Max number of items to return (server-side)")] int? top = 50,
        [Description("Optional: Comma-separated list of fields to return (default: summary fields)")] string? fieldsCsv = null,
        [Description("Optional: Include relations (default: false)")] bool includeRelations = false,
        [Description("Optional: Skip N items from the matched set before returning page")] int skip = 0,
        [Description("Optional: Page size of returned work items")] int pageSize = 50,
        [Description("Project name or id (opcional)")] string? project = null)
    {
        try
        {
            var client = await _clientFactory.GetClientAsync<WorkItemTrackingHttpClient>();
            var query = new Wiql { Query = wiql };
            var resolvedProject = RequireProjectOrDefault(project);
            var result = await client.QueryByWiqlAsync(query, resolvedProject, top: top);
            var ids = result.WorkItems.Select(w => w.Id).ToList();

            if (ids.Count == 0)
            {
                return new AzdoOperationResult(true, data: new { Count = 0, WorkItems = Array.Empty<object>() });
            }

            var defaultFields = new[]
            {
                "System.Id", "System.WorkItemType", "System.TeamProject", "System.Title",
                "System.State", "System.AssignedTo", "System.Tags", "System.ChangedDate", "System.CreatedDate"
            };
            var fields = string.IsNullOrWhiteSpace(fieldsCsv)
                ? defaultFields
                : fieldsCsv.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

            if (skip < 0) skip = 0;
            if (pageSize <= 0) pageSize = 50;
            var pageIds = ids.Skip(skip).Take(pageSize).ToList();

            var expand = includeRelations ? WorkItemExpand.Relations : WorkItemExpand.None;
            var wis = await client.GetWorkItemsAsync(pageIds, fields, expand: expand);
            var items = wis.Select(w => new
            {
                w.Id,
                w.Url,
                w.Rev,
                Fields = w.Fields,
                Relations = includeRelations ? w.Relations?.Select(r => new { r.Rel, r.Url, r.Attributes }).ToList() : null
            }).ToList();

            var nextSkip = skip + pageIds.Count;
            var data = new
            {
                TotalMatched = ids.Count,
                Returned = items.Count,
                Skip = skip,
                PageSize = pageSize,
                NextSkip = nextSkip < ids.Count ? nextSkip : (int?)null,
                Fields = fields,
                WorkItems = items
            };

            return new AzdoOperationResult(true, data: data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "QueryWorkItems failed: {Message}", ex.Message);
            return new AzdoOperationResult(false, ex.Message);
        }
    }

    [McpServerTool(
        Title = "Get Work Item",
        ReadOnly = true,
        Idempotent = true,
        Destructive = false),
        Description("Gets a single work item by id, including all fields and relations.")]
    public async Task<AzdoOperationResult> GetWorkItem(
        [Description("Work item id")] int id)
    {
        try
        {
            var client = await _clientFactory.GetClientAsync<WorkItemTrackingHttpClient>();
            var wi = await client.GetWorkItemAsync(id, expand: WorkItemExpand.All);
            var data = new
            {
                wi.Id,
                wi.Url,
                wi.Rev,
                Fields = wi.Fields,
                Relations = wi.Relations?.Select(r => new { r.Rel, r.Url, r.Attributes })
            };
            return new AzdoOperationResult(true, data: data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetWorkItem failed: {Message}", ex.Message);
            return new AzdoOperationResult(false, ex.Message);
        }
    }
}
