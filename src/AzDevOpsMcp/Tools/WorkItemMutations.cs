using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.WebApi.Patch;
using Microsoft.VisualStudio.Services.WebApi.Patch.Json;
using ModelContextProtocol.Server;

namespace AzDevOps.McpServer;

public partial class Tools
{
    [McpServerTool(
        Title = "Create Work Item",
        ReadOnly = false,
        Destructive = false),
        Description("Creates a work item with an MCP-friendly contract and project-specific type resolution.")]
    public async Task<OperationResult> CreateWorkItem(
        [Description("Work item type or intent, e.g. 'Bug', 'Task', 'User Story', 'Product Backlog Item'")] string type,
        [Description("Fields to set on the work item as a JSON object. Required: System.Title. Example: {\"System.Title\":\"My bug\",\"System.Description\":\"Details\"}")] Dictionary<string, JsonElement>? fields = null,
        [Description("Optional work item id to assign as the parent through a hierarchy relation")] int? parentId = null,
        [Description("Optional: Validate only without saving the work item")] bool validateOnly = false,
        [Description("Project name or id (opcional)")] string? project = null)
    {
        string? resolvedProject = null;

        try
        {
            var client = await _clientFactory.GetClientAsync<WorkItemTrackingHttpClient>();
            resolvedProject = RequireProjectOrDefault(project);
            var availableTypes = await client.GetWorkItemTypesAsync(resolvedProject);
            var resolvedType = WorkItemMutationSupport.ResolveTypeName(type, availableTypes.Select(workItemType => workItemType.Name).Where(name => !string.IsNullOrWhiteSpace(name)).ToList(), out var typeWarning);
            var fieldDict = WorkItemMutationSupport.BuildFields(fields);
            string? parentUrl = null;

            if (parentId.HasValue)
            {
                WorkItemMutationSupport.ValidateParentId(parentId.Value);
                var parent = await client.GetWorkItemAsync(resolvedProject, parentId.Value);
                parentUrl = parent.Url ?? throw new ArgumentException($"Parent work item '{parentId.Value}' does not expose a canonical URL.");
            }

            var patch = WorkItemMutationSupport.ToPatchDocument(fieldDict, parentUrl: parentUrl);
            var created = await client.CreateWorkItemAsync(patch, resolvedProject, resolvedType, validateOnly: validateOnly);
            var warnings = typeWarning is null ? [] : new[] { typeWarning };
            var data = WorkItemMutationSupport.ToMutationResponse("create", resolvedProject, type, resolvedType, validateOnly, created, warnings);
            return new OperationResult(true, data: data);
        }
        catch (ArgumentException ex)
        {
            return new OperationResult(false, ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return new OperationResult(false, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CreateWorkItem failed: {Message}", ex.Message);
            return new OperationResult(false, WorkItemMutationSupport.BuildMutationFailureMessage("create", resolvedProject ?? project ?? "<not configured>", type));
        }
    }

    [McpServerTool(
        Title = "List Work Item Types",
        ReadOnly = true,
        Idempotent = true,
        Destructive = false),
        Description("Lists valid work item types for a project so create tools can use the exact process-specific type name.")]
    public async Task<OperationResult> ListWorkItemTypes(
        [Description("Project name or id (optional, uses default if configured)")] string? project = null)
    {
        try
        {
            var client = await _clientFactory.GetClientAsync<WorkItemTrackingHttpClient>();
            var resolvedProject = RequireProjectOrDefault(project);
            var workItemTypes = await client.GetWorkItemTypesAsync(resolvedProject);
            var data = new
            {
                Project = resolvedProject,
                Count = workItemTypes.Count,
                Types = workItemTypes.Select(workItemType => new
                {
                    workItemType.Name,
                    workItemType.ReferenceName,
                    workItemType.Description,
                    workItemType.Color,
                    workItemType.IsDisabled
                }).ToList()
            };

            return new OperationResult(true, data: data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ListWorkItemTypes failed: {Message}", ex.Message);
            return new OperationResult(false, "Unable to list work item types for the requested project.");
        }
    }

    [McpServerTool(
        Title = "Update Work Item",
        ReadOnly = false,
        Destructive = true),
        Description("Updates a work item using MCP-friendly fields and optional revision protection.")]
    public async Task<OperationResult> UpdateWorkItem(
        [Description("Work item id")] int id,
        [Description("Fields to update as a JSON object. Example: {\"System.State\":\"Active\",\"System.Title\":\"New title\"}")] Dictionary<string, JsonElement>? fields = null,
        [Description("Optional history entry stored in System.History")] string? history = null,
        [Description("Optional work item id to assign as the parent through a hierarchy relation")] int? parentId = null,
        [Description("Optional current revision for optimistic concurrency via test /rev")] int? rev = null,
        [Description("Optional: Validate only without saving the update")] bool validateOnly = false,
        [Description("Project name or id (optional, uses default if configured)")] string? project = null)
    {
        string? resolvedProject = null;

        try
        {
            var client = await _clientFactory.GetClientAsync<WorkItemTrackingHttpClient>();
            resolvedProject = RequireProjectOrDefault(project);
            WorkItemMutationSupport.ValidateWorkItemId(id);
            WorkItemMutationSupport.ValidateRevision(rev);
            var fieldDict = WorkItemMutationSupport.BuildFields(fields, history, allowEmpty: parentId.HasValue);
            WorkItem? currentWorkItem = null;
            string? parentUrl = null;

            if (parentId.HasValue)
            {
                WorkItemMutationSupport.ValidateParentId(parentId.Value, id);
                var parent = await client.GetWorkItemAsync(resolvedProject, parentId.Value);
                var canonicalParentUrl = parent.Url ?? throw new ArgumentException($"Parent work item '{parentId.Value}' does not expose a canonical URL.");
                currentWorkItem = await client.GetWorkItemAsync(resolvedProject, id, expand: WorkItemExpand.Relations);

                if (WorkItemMutationSupport.ShouldAddParentRelation(currentWorkItem.Relations?.ToList(), canonicalParentUrl))
                {
                    parentUrl = canonicalParentUrl;
                }
            }

            WorkItem updated;
            if (fieldDict.Count == 0 && parentUrl is null)
            {
                WorkItemMutationSupport.ValidateRevision(rev, currentWorkItem!.Rev);
                updated = currentWorkItem!;
            }
            else
            {
                var patch = WorkItemMutationSupport.ToPatchDocument(fieldDict, rev, parentUrl);
                updated = await client.UpdateWorkItemAsync(patch, resolvedProject, id, validateOnly: validateOnly);
            }

            var data = WorkItemMutationSupport.ToUpdateResponse(resolvedProject, id, validateOnly, rev, updated);
            return new OperationResult(true, data: data);
        }
        catch (ArgumentException ex)
        {
            return new OperationResult(false, ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return new OperationResult(false, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UpdateWorkItem failed: {Message}", ex.Message);
            return new OperationResult(false, WorkItemMutationSupport.BuildMutationFailureMessage("update", resolvedProject ?? project ?? "<not configured>", workItemId: id));
        }
    }
}
