using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace AzDevOps.McpServer;

[McpServerToolType]
public partial class Tools(IDevOpsClientFactory clientFactory, IDevOpsProjectContext projectContext, ILogger<Tools> logger)
{
    private readonly IDevOpsClientFactory _clientFactory = clientFactory;
    private readonly IDevOpsProjectContext _projectContext = projectContext;
    private readonly ILogger<Tools> _logger = logger;

    private string RequireProjectOrDefault(string? project)
    {
        if (!string.IsNullOrWhiteSpace(project)) return project;
        if (!string.IsNullOrWhiteSpace(_projectContext.DefaultProject)) return _projectContext.DefaultProject!;
        throw new InvalidOperationException("Project not specified. Provide the 'project' parameter or set AZDO_PROJECT/--project.");
    }
}
