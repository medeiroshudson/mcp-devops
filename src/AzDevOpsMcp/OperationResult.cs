namespace AzDevOps.McpServer;

public class OperationResult(bool success, string? error = null, object? data = null)
{
    public bool Success { get; } = success;
    public string? Error { get; } = error;
    public object? Data { get; } = data;
}
