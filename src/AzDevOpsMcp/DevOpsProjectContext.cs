using Microsoft.Extensions.Configuration;

namespace AzDevOps.McpServer;

public interface IDevOpsProjectContext
{
    string? DefaultProject { get; }
}

public class DevOpsProjectContext(IConfiguration configuration) : IDevOpsProjectContext
{
    public string? DefaultProject { get; } = ResolveDefaultProject(configuration);

    private static string? ResolveDefaultProject(IConfiguration configuration)
    {
        var environmentProject = Normalize(configuration["AZDO_PROJECT"]);
        if (environmentProject is not null)
        {
            return environmentProject;
        }

        return Normalize(configuration["project"]);
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
