using Microsoft.Extensions.Configuration;

namespace AzDevOps.McpServer;

public interface IDevOpsProjectContext
{
    string? DefaultProject { get; }
}

public class DevOpsProjectContext(IConfiguration configuration) : IDevOpsProjectContext
{
    public string? DefaultProject { get; } = configuration["AZDO_PROJECT"];
}
