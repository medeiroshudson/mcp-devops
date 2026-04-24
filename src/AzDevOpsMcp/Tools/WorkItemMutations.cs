using System.ComponentModel;
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
        [Description("Title for the work item. Required.")] string title,
        [Description("Optional description stored in System.Description")] string? description = null,
        [Description("Optional JSON object of additional field reference names and values. Example: {\"Microsoft.VSTS.Common.Priority\":1}")] string? fieldsJson = null,
        [Description("Optional: Validate only without saving the work item")] bool validateOnly = false,
        [Description("Project name or id (opcional)")] string? project = null)
    {
        try
        {
            var client = await _clientFactory.GetClientAsync<WorkItemTrackingHttpClient>();
            var resolvedProject = RequireProjectOrDefault(project);
            var availableTypes = await client.GetWorkItemTypesAsync(resolvedProject);
            var resolvedType = WorkItemMutationSupport.ResolveTypeName(type, availableTypes.Select(workItemType => workItemType.Name).Where(name => !string.IsNullOrWhiteSpace(name)).ToList(), out var typeWarning);
            var fields = WorkItemMutationSupport.BuildCreateFields(title, description, fieldsJson);
            var patch = WorkItemMutationSupport.ToPatchDocument(fields);
            var created = await client.CreateWorkItemAsync(patch, resolvedProject, resolvedType, validateOnly: validateOnly);
            var warnings = typeWarning is null ? [] : new[] { typeWarning };
            var data = WorkItemMutationSupport.ToMutationResponse("create", resolvedProject, type, resolvedType, validateOnly, created, warnings);
            return new OperationResult(true, data: data);
        }
        catch (ArgumentException ex)
        {
            return new OperationResult(false, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CreateWorkItem failed: {Message}", ex.Message);
            return new OperationResult(false, WorkItemMutationSupport.BuildMutationFailureMessage("create", RequireProjectOrDefault(project), type));
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
        Destructive = false),
        Description("Updates a work item using MCP-friendly fields and optional revision protection.")]
    public async Task<OperationResult> UpdateWorkItem(
        [Description("Work item id")] int id,
        [Description("Optional title update stored in System.Title")] string? title = null,
        [Description("Optional description update stored in System.Description")] string? description = null,
        [Description("Optional history entry stored in System.History")] string? history = null,
        [Description("Optional JSON object of additional field reference names and values. Example: {\"System.State\":\"Active\"}")] string? fieldsJson = null,
        [Description("Optional current revision for optimistic concurrency via test /rev")] int? rev = null,
        [Description("Optional: Validate only without saving the update")] bool validateOnly = false,
        [Description("Project name or id (optional, uses default if configured)")] string? project = null)
    {
        try
        {
            var client = await _clientFactory.GetClientAsync<WorkItemTrackingHttpClient>();
            var resolvedProject = RequireProjectOrDefault(project);
            var fields = WorkItemMutationSupport.BuildUpdateFields(title, description, history, fieldsJson);
            var patch = WorkItemMutationSupport.ToPatchDocument(fields, rev);
            var updated = await client.UpdateWorkItemAsync(patch, id, validateOnly: validateOnly);
            var data = WorkItemMutationSupport.ToUpdateResponse(resolvedProject, id, validateOnly, rev, updated);
            return new OperationResult(true, data: data);
        }
        catch (ArgumentException ex)
        {
            return new OperationResult(false, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UpdateWorkItem failed: {Message}", ex.Message);
            return new OperationResult(false, WorkItemMutationSupport.BuildMutationFailureMessage("update", RequireProjectOrDefault(project), workItemId: id));
        }
    }
}
