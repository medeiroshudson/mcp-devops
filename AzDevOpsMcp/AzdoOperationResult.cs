namespace AzDevOps.McpServer;

public class AzdoOperationResult(bool success, string? error = null, object? data = null)
{
    public bool Success { get; } = success;
    public string? Error { get; } = error;
    public object? Data { get; } = data;
}

