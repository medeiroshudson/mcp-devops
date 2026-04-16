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
    public async Task<OperationResult> QueryWorkItems(
        [Description("WIQL query string")] string wiql,
        [Description("Optional: Max number of items to return from the WIQL result set before paging details (default: 50, max: 200)")] int? top = WorkItemQueryDefaults.DefaultTop,
        [Description("Optional: Comma-separated list of fields to return (default: summary fields)")] string? fieldsCsv = null,
        [Description("Optional: Include relations (default: false)")] bool includeRelations = false,
        [Description("Optional: Skip N items from the matched WIQL result before returning the current page")] int skip = 0,
        [Description("Optional: Page size of returned work items (default: 50, max: 200)")] int pageSize = WorkItemQueryDefaults.DefaultPageSize,
        [Description("Project name or id (opcional)")] string? project = null)
    {
        try
        {
            var wiqlValidationError = WorkItemQueryPaging.ValidateWiql(wiql);
            if (wiqlValidationError is not null)
            {
                return new OperationResult(false, wiqlValidationError);
            }

            var paging = WorkItemQueryPaging.Normalize(top, skip, pageSize);
            var client = await _clientFactory.GetClientAsync<WorkItemTrackingHttpClient>();
            var query = new Wiql { Query = wiql };
            var resolvedProject = RequireProjectOrDefault(project);
            var result = await client.QueryByWiqlAsync(query, resolvedProject, top: paging.Top);
            var queryType = result.QueryType.ToString();
            var relationReferences = result.WorkItemRelations?
                .Select(relation => (object)new
                {
                    SourceId = relation.Source?.Id,
                    TargetId = relation.Target?.Id,
                    relation.Rel
                })
                .ToList();
            var ids = result.WorkItems?.Select(w => w.Id).ToList() ?? [];
            var fields = WorkItemProjection.ResolveFields(fieldsCsv);

            var warnings = paging.Warnings.ToList();
            var truncated = ids.Count == paging.Top;
            if (truncated)
            {
                warnings.Add(new WorkItemQueryWarning("top_boundary_reached", "The WIQL result reached the configured top limit. Additional matching items may exist."));
            }

            if (result.QueryType != QueryType.Flat)
            {
                warnings.Add(new WorkItemQueryWarning("non_flat_query", "This WIQL query returned link-based results. Relation references are included and work item details are omitted."));
                return new OperationResult(true, data: WorkItemQueryResponse.Empty(
                    queryType,
                    paging.Skip,
                    paging.PageSize,
                    fields,
                    includeRelations,
                    warnings,
                    totalMatched: relationReferences?.Count ?? 0,
                    truncated: truncated,
                    relationReferences: relationReferences));
            }

            if (ids.Count == 0)
            {
                return new OperationResult(true, data: WorkItemQueryResponse.Empty(queryType, paging.Skip, paging.PageSize, fields, includeRelations, warnings, truncated: truncated));
            }

            var pageIds = ids.Skip(paging.Skip).Take(paging.PageSize).ToList();

            if (pageIds.Count == 0)
            {
                warnings.Add(new WorkItemQueryWarning("empty_page", "The requested page is beyond the matched WIQL result window."));
                return new OperationResult(true, data: WorkItemQueryResponse.Empty(queryType, paging.Skip, paging.PageSize, fields, includeRelations, warnings, ids.Count, truncated));
            }

            var expand = includeRelations ? WorkItemExpand.Relations : WorkItemExpand.None;
            var wis = await GetWorkItemsInChunksAsync(client, pageIds, fields, expand);
            var items = wis.Select(w => WorkItemProjection.Project(w, fields, includeRelations)).ToList();
            var omittedIds = pageIds.Except(wis.Select(w => w.Id ?? 0)).Where(id => id > 0).ToList();

            if (omittedIds.Count > 0)
            {
                warnings.Add(new WorkItemQueryWarning("omitted_ids", $"{omittedIds.Count} work item(s) were not returned by Azure DevOps for this page."));
            }

            var nextSkip = paging.GetNextSkip(ids.Count);
            var data = new WorkItemQueryResponse
            {
                QueryType = queryType,
                TotalMatched = ids.Count,
                Returned = items.Count,
                Skip = paging.Skip,
                PageSize = paging.PageSize,
                NextSkip = nextSkip,
                RequestedFields = fields,
                IncludeRelations = includeRelations,
                Truncated = truncated,
                Warnings = warnings,
                OmittedIds = omittedIds,
                RelationReferences = null,
                WorkItems = items
            };

            return new OperationResult(true, data: data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "QueryWorkItems failed: {Message}", ex.Message);
            return new OperationResult(false, "Unable to query work items.");
        }
    }

    [McpServerTool(
        Title = "Get Work Item",
        ReadOnly = true,
        Idempotent = true,
        Destructive = false),
        Description("Gets a single work item by id, including all fields and relations.")]
    public async Task<OperationResult> GetWorkItem(
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
            return new OperationResult(true, data: data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetWorkItem failed: {Message}", ex.Message);
            return new OperationResult(false, "Unable to get the requested work item.");
        }
    }

    private static async Task<List<WorkItem>> GetWorkItemsInChunksAsync(
        WorkItemTrackingHttpClient client,
        IReadOnlyList<int> ids,
        IEnumerable<string> fields,
        WorkItemExpand expand)
    {
        var results = new List<WorkItem>();

        foreach (var chunk in ids.Chunk(WorkItemQueryDefaults.MaxPageSize))
        {
            var chunkItems = await client.GetWorkItemsAsync(chunk, fields, expand: expand);
            results.AddRange(chunkItems);
        }

        return results;
    }
}
