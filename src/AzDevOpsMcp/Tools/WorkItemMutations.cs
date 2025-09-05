using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
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
        Description("Creates a work item of a given type with provided fields.")]
    public async Task<OperationResult> CreateWorkItem(
        [Description("Work item type, e.g., 'Bug', 'Task', 'User Story'")] string type,
        [Description("Fields to set. Example: { 'System.Title': 'My bug', 'System.Description': 'Details' }")] Dictionary<string, object> fields,
        [Description("Project name or id (opcional)")] string? project = null)
    {
        try
        {
            var client = await _clientFactory.GetClientAsync<WorkItemTrackingHttpClient>();
            var resolvedProject = RequireProjectOrDefault(project);
            var patch = ToPatchDocument(fields);
            var created = await client.CreateWorkItemAsync(patch, resolvedProject, type);
            var data = new { created.Id, created.Url, created.Rev, created.Fields };
            return new OperationResult(true, data: data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CreateWorkItem failed: {Message}", ex.Message);
            return new OperationResult(false, ex.Message);
        }
    }

    [McpServerTool(
        Title = "Update Work Item",
        ReadOnly = false,
        Destructive = false),
        Description("Updates fields of an existing work item.")]
    public async Task<OperationResult> UpdateWorkItem(
        [Description("Work item id")] int id,
        [Description("Fields to update. Example: { 'System.State': 'Active' }")] Dictionary<string, object> fields)
    {
        try
        {
            var client = await _clientFactory.GetClientAsync<WorkItemTrackingHttpClient>();
            var patch = ToPatchDocument(fields);
            var updated = await client.UpdateWorkItemAsync(patch, id);
            var data = new { updated.Id, updated.Url, updated.Rev, updated.Fields };
            return new OperationResult(true, data: data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UpdateWorkItem failed: {Message}", ex.Message);
            return new OperationResult(false, ex.Message);
        }
    }

    private static JsonPatchDocument ToPatchDocument(Dictionary<string, object> fields)
    {
        var patch = new JsonPatchDocument();
        foreach (var kvp in fields)
        {
            patch.Add(new JsonPatchOperation
            {
                Operation = Operation.Add,
                Path = "/fields/" + kvp.Key,
                Value = kvp.Value
            });
        }
        return patch;
    }
}
