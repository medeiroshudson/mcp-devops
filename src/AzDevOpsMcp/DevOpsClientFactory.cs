using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;

namespace AzDevOps.McpServer;

public interface IDevOpsClientFactory
{
    Task<VssConnection> GetConnectionAsync();
    Task<T> GetClientAsync<T>() where T : VssHttpClientBase;
}

public class DevOpsClientFactory : IDevOpsClientFactory
{
    private VssConnection? _cachedConnection;

    public async Task<VssConnection> GetConnectionAsync()
    {
        if (_cachedConnection is not null)
        {
            return _cachedConnection;
        }

        var orgUrl = Environment.GetEnvironmentVariable("AZDO_ORG_URL");
        var pat = Environment.GetEnvironmentVariable("AZDO_PAT");

        if (string.IsNullOrWhiteSpace(orgUrl))
        {
            throw new InvalidOperationException("AZDO_ORG_URL is not set. Example: https://dev.azure.com/<organization>");
        }
        if (string.IsNullOrWhiteSpace(pat))
        {
            throw new InvalidOperationException("AZDO_PAT is not set. Create a Personal Access Token in Azure DevOps and set AZDO_PAT.");
        }

        var creds = new VssBasicCredential(string.Empty, pat);
        var connection = new VssConnection(new Uri(orgUrl), creds);

        await connection.ConnectAsync();

        _cachedConnection = connection;
        return connection;
    }

    public async Task<T> GetClientAsync<T>() where T : VssHttpClientBase
    {
        var connection = await GetConnectionAsync();
        return connection.GetClient<T>();
    }
}
