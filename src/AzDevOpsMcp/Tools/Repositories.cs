using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using ModelContextProtocol.Server;

namespace AzDevOps.McpServer;

public partial class Tools
{
    [McpServerTool(
        Title = "List Repositories",
        ReadOnly = true,
        Idempotent = true,
        Destructive = false),
        Description("Lists Git repositories for a given project.")]
    public async Task<AzdoOperationResult> ListRepositories(
        [Description("Project name or id (optional, usa padrão se definido)")] string? project = null)
    {
        try
        {
            var client = await _clientFactory.GetClientAsync<GitHttpClient>();
            var resolvedProject = RequireProjectOrDefault(project);
            var repos = await client.GetRepositoriesAsync(resolvedProject);
            var data = repos.Select(r => new { r.Id, r.Name, r.ProjectReference, r.DefaultBranch, r.RemoteUrl }).ToList();
            return new AzdoOperationResult(true, data: data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ListRepositories failed: {Message}", ex.Message);
            return new AzdoOperationResult(false, ex.Message);
        }
    }
}
