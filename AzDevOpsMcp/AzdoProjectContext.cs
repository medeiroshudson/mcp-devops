using Microsoft.Extensions.Configuration;

namespace AzDevOps.McpServer;

public interface IAzdoProjectContext
{
    string? DefaultProject { get; }
}

public class AzdoProjectContext : IAzdoProjectContext
{
    public string? DefaultProject { get; }

    public AzdoProjectContext(IConfiguration configuration)
    {
        // Priority: command line --project, env AZDO_PROJECT, fallback PROJECT
        DefaultProject = configuration["project"]
                          ?? configuration["AZDO_PROJECT"]
                          ?? configuration["PROJECT"];
    }
}

