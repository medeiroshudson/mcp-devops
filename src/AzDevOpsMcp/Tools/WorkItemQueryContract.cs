namespace AzDevOps.McpServer;

internal static class WorkItemQueryDefaults
{
    public const int DefaultTop = 50;
    public const int DefaultPageSize = 50;
    public const int MaxPageSize = 200;
    public const int MaxTop = 200;
    public const int MaxWiqlLength = 32_000;

    public static readonly string[] DefaultFields =
    {
        "System.Id",
        "System.WorkItemType",
        "System.Title",
        "System.State",
        "System.AssignedTo",
        "System.ChangedDate"
    };
}

internal sealed record WorkItemQueryWarning(string Code, string Message);

internal sealed class WorkItemQueryResponse
{
    public required string QueryType { get; init; }
    public required int TotalMatched { get; init; }
    public required int Returned { get; init; }
    public required int Skip { get; init; }
    public required int PageSize { get; init; }
    public int? NextSkip { get; init; }
    public required IReadOnlyList<string> RequestedFields { get; init; }
    public required bool IncludeRelations { get; init; }
    public required bool Truncated { get; init; }
    public required IReadOnlyList<WorkItemQueryWarning> Warnings { get; init; }
    public required IReadOnlyList<int> OmittedIds { get; init; }
    public IReadOnlyList<object>? RelationReferences { get; init; }
    public required IReadOnlyList<object> WorkItems { get; init; }

    public static WorkItemQueryResponse Empty(
        string queryType,
        int skip,
        int pageSize,
        IReadOnlyList<string> requestedFields,
        bool includeRelations,
        IReadOnlyList<WorkItemQueryWarning>? warnings = null,
        int totalMatched = 0,
        bool truncated = false,
        IReadOnlyList<int>? omittedIds = null,
        IReadOnlyList<object>? relationReferences = null) =>
        new()
        {
            QueryType = queryType,
            TotalMatched = totalMatched,
            Returned = 0,
            Skip = skip,
            PageSize = pageSize,
            NextSkip = null,
            RequestedFields = requestedFields,
            IncludeRelations = includeRelations,
            Truncated = truncated,
            Warnings = warnings ?? [],
            OmittedIds = omittedIds ?? [],
            RelationReferences = relationReferences,
            WorkItems = []
        };
}
