using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace AzDevOps.McpServer;

[McpServerToolType]
public partial class Tools(IAzdoClientFactory clientFactory, IAzdoProjectContext projectContext, ILogger<Tools> logger)
{
    private readonly IAzdoClientFactory _clientFactory = clientFactory;
    private readonly IAzdoProjectContext _projectContext = projectContext;
    private readonly ILogger<Tools> _logger = logger;

    private string RequireProjectOrDefault(string? project)
    {
        if (!string.IsNullOrWhiteSpace(project)) return project;
        if (!string.IsNullOrWhiteSpace(_projectContext.DefaultProject)) return _projectContext.DefaultProject!;
        throw new InvalidOperationException("Projeto não especificado. Informe o parâmetro 'project' ou defina AZDO_PROJECT/--project.");
    }
}
