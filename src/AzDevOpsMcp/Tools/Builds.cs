using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.Build.WebApi;
using ModelContextProtocol.Server;

namespace AzDevOps.McpServer;

public partial class Tools
{
    [McpServerTool(
        Title = "List Build Definitions",
        ReadOnly = true,
        Idempotent = true,
        Destructive = false),
        Description("Lists build definitions (pipelines) in a project.")]
    public async Task<OperationResult> ListBuildDefinitions(
        [Description("Project name or id (opcional)")] string? project = null)
    {
        try
        {
            var client = await _clientFactory.GetClientAsync<BuildHttpClient>();
            var resolvedProject = RequireProjectOrDefault(project);
            var defs = await client.GetDefinitionsAsync(resolvedProject);
            var data = defs.Select(d => new { d.Id, d.Name, Path = d.Path }).ToList();
            return new OperationResult(true, data: data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ListBuildDefinitions failed: {Message}", ex.Message);
            return new OperationResult(false, ex.Message);
        }
    }

    [McpServerTool(
        Title = "Queue Build",
        ReadOnly = false,
        Destructive = false),
        Description("Queues a build for a given definition in a project.")]
    public async Task<OperationResult> QueueBuild(
        [Description("Build definition id")] int definitionId,
        [Description("Optional: Branch to build (e.g., refs/heads/main)")] string? sourceBranch = null,
        [Description("Project name or id (opcional)")] string? project = null)
    {
        try
        {
            var client = await _clientFactory.GetClientAsync<BuildHttpClient>();
            var resolvedProject = RequireProjectOrDefault(project);
            var build = new Build
            {
                Definition = new DefinitionReference { Id = definitionId },
                Project = new Microsoft.TeamFoundation.Core.WebApi.TeamProjectReference { Name = resolvedProject },
                SourceBranch = sourceBranch
            };
            var queued = await client.QueueBuildAsync(build, resolvedProject);
            var data = new { queued.Id, queued.BuildNumber, queued.Status, queued.Result, queued.SourceBranch, queued.QueueTime, DefinitionId = queued.Definition?.Id, DefinitionName = queued.Definition?.Name };
            return new OperationResult(true, data: data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "QueueBuild failed: {Message}", ex.Message);
            return new OperationResult(false, ex.Message);
        }
    }
}
