using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.Core.WebApi;
using ModelContextProtocol.Server;

namespace AzDevOps.McpServer;

public partial class Tools
{
    [McpServerTool(
        Title = "List Projects",
        ReadOnly = true,
        Idempotent = true,
        Destructive = false),
        Description("Lists all Azure DevOps projects visible to the PAT.")]
    public async Task<OperationResult> ListProjects(
        [Description("Optional: Continuation token for paging")] string? continuationToken = null,
        [Description("Optional: Number of projects to fetch")] int? top = 100)
    {
        try
        {
            var client = await _clientFactory.GetClientAsync<ProjectHttpClient>();
            var result = await client.GetProjects(top: top, continuationToken: continuationToken);
            var data = new
            {
                Count = result.Count,
                Projects = result.Select(p => new { p.Id, p.Name, p.Description, p.Url, p.State }).ToList(),
                ContinuationToken = result.ContinuationToken
            };
            return new OperationResult(true, data: data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ListProjects failed: {Message}", ex.Message);
            return new OperationResult(false, ex.Message);
        }
    }
}
