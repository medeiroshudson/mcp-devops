namespace AzDevOps.McpServer;

internal sealed class WorkItemQueryPagingState
{
    public required int Top { get; init; }
    public required int Skip { get; init; }
    public required int PageSize { get; init; }
    public required IReadOnlyList<WorkItemQueryWarning> Warnings { get; init; }

    public int? GetNextSkip(int totalMatched)
    {
        if (Skip >= totalMatched)
        {
            return null;
        }

        var nextSkip = Skip + PageSize;
        return nextSkip < totalMatched ? nextSkip : null;
    }
}

internal static class WorkItemQueryPaging
{
    public static string? ValidateWiql(string wiql)
    {
        if (string.IsNullOrWhiteSpace(wiql))
        {
            return "WIQL query is required.";
        }

        if (wiql.Length > WorkItemQueryDefaults.MaxWiqlLength)
        {
            return $"WIQL query exceeds the supported limit of {WorkItemQueryDefaults.MaxWiqlLength} characters.";
        }

        var normalizedWiql = wiql.ToUpperInvariant();
        if (normalizedWiql.Contains("FROM WORKITEMLINKS", StringComparison.Ordinal) && normalizedWiql.Contains("ORDER BY", StringComparison.Ordinal))
        {
            return "WIQL queries that use WorkItemLinks with ORDER BY are not supported by this tool.";
        }

        return null;
    }

    public static WorkItemQueryPagingState Normalize(int? top, int skip, int pageSize)
    {
        var warnings = new List<WorkItemQueryWarning>();

        var normalizedTop = top ?? WorkItemQueryDefaults.DefaultTop;
        if (normalizedTop <= 0)
        {
            normalizedTop = WorkItemQueryDefaults.DefaultTop;
            warnings.Add(new WorkItemQueryWarning("top_defaulted", $"The requested top value was invalid and was reset to {normalizedTop}."));
        }
        else if (normalizedTop > WorkItemQueryDefaults.MaxTop)
        {
            normalizedTop = WorkItemQueryDefaults.MaxTop;
            warnings.Add(new WorkItemQueryWarning("top_capped", $"The requested top value exceeded the limit and was capped at {normalizedTop}."));
        }

        var normalizedSkip = Math.Max(0, skip);
        if (normalizedSkip != skip)
        {
            warnings.Add(new WorkItemQueryWarning("skip_normalized", "The requested skip value was below zero and was reset to 0."));
        }

        var normalizedPageSize = pageSize <= 0 ? WorkItemQueryDefaults.DefaultPageSize : Math.Min(pageSize, WorkItemQueryDefaults.MaxPageSize);
        if (pageSize <= 0)
        {
            warnings.Add(new WorkItemQueryWarning("page_size_defaulted", $"The requested pageSize was invalid and was reset to {normalizedPageSize}."));
        }
        else if (pageSize > WorkItemQueryDefaults.MaxPageSize)
        {
            warnings.Add(new WorkItemQueryWarning("page_size_capped", $"The requested pageSize exceeded the limit and was capped at {normalizedPageSize}."));
        }

        return new WorkItemQueryPagingState
        {
            Top = normalizedTop,
            Skip = normalizedSkip,
            PageSize = normalizedPageSize,
            Warnings = warnings
        };
    }
}
