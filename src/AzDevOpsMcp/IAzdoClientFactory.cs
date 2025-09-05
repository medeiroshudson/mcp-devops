using Microsoft.VisualStudio.Services.WebApi;

namespace AzDevOps.McpServer;

public interface IAzdoClientFactory
{
    Task<VssConnection> GetConnectionAsync();
    Task<T> GetClientAsync<T>() where T : VssHttpClientBase;
}
