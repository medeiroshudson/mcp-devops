using System.Text.Json;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.WebApi.Patch;
using Microsoft.VisualStudio.Services.WebApi.Patch.Json;

namespace AzDevOps.McpServer;

internal static class WorkItemMutationSupport
{
    private const string ParentRelationType = "System.LinkTypes.Hierarchy-Reverse";

    private static readonly string[] RequirementTypePreferenceOrder =
    {
        "User Story",
        "Product Backlog Item",
        "Requirement",
        "Issue"
    };

    private static readonly HashSet<string> GenericRequirementAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        "user story",
        "userstory",
        "story",
        "backlog",
        "backlog item",
        "backlogitem",
        "product backlog item",
        "pbi",
        "requirement",
        "issue"
    };

    public static string ResolveTypeName(string requestedType, IReadOnlyList<string> availableTypeNames, out string? warning)
    {
        if (string.IsNullOrWhiteSpace(requestedType))
        {
            throw new ArgumentException("Work item type is required.");
        }

        var exactMatch = availableTypeNames.FirstOrDefault(typeName => string.Equals(typeName, requestedType, StringComparison.OrdinalIgnoreCase));
        if (exactMatch is not null)
        {
            warning = null;
            return exactMatch;
        }

        if (GenericRequirementAliases.Contains(requestedType.Trim()))
        {
            foreach (var preferredType in RequirementTypePreferenceOrder)
            {
                var resolvedType = availableTypeNames.FirstOrDefault(typeName => string.Equals(typeName, preferredType, StringComparison.OrdinalIgnoreCase));
                if (resolvedType is not null)
                {
                    warning = string.Equals(resolvedType, requestedType, StringComparison.OrdinalIgnoreCase)
                        ? null
                        : $"Requested type '{requestedType}' was resolved to '{resolvedType}' for the target project's process.";
                    return resolvedType;
                }
            }
        }

        throw new ArgumentException($"Work item type '{requestedType}' is not valid for the target project. Use ListWorkItemTypes to inspect available types.");
    }

    public static IReadOnlyDictionary<string, object?> BuildFields(
        IReadOnlyDictionary<string, JsonElement>? fields,
        string? history = null,
        bool allowEmpty = false)
    {
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        if (fields is not null)
        {
            foreach (var field in fields)
            {
                result[field.Key] = ConvertJsonElement(field.Value);
            }
        }

        if (!string.IsNullOrWhiteSpace(history))
        {
            result["System.History"] = history.Trim();
        }

        if (result.Count == 0 && !allowEmpty)
        {
            throw new ArgumentException("At least one field is required. Pass a JSON object with field reference names as keys, e.g. {\"System.Title\":\"My title\"}.");
        }

        return result;
    }

    public static void ValidateParentId(int parentId, int? childId = null)
    {
        if (parentId <= 0)
        {
            throw new ArgumentException("parentId must be greater than zero.");
        }

        if (childId.HasValue && parentId == childId.Value)
        {
            throw new ArgumentException("A work item cannot be its own parent.");
        }
    }

    public static void ValidateWorkItemId(int id)
    {
        if (id <= 0)
        {
            throw new ArgumentException("Work item id must be greater than zero.");
        }
    }

    public static void ValidateRevision(int? expectedRevision, int? actualRevision = null)
    {
        if (expectedRevision < 0)
        {
            throw new ArgumentException("rev must be zero or greater.");
        }

        if (expectedRevision.HasValue && actualRevision.HasValue && expectedRevision.Value != actualRevision.Value)
        {
            throw new ArgumentException($"Revision guard '{expectedRevision.Value}' does not match the current revision '{actualRevision.Value}'.");
        }
    }

    public static bool ShouldAddParentRelation(IReadOnlyList<WorkItemRelation>? relations, string parentUrl)
    {
        if (string.IsNullOrWhiteSpace(parentUrl))
        {
            throw new ArgumentException("The parent work item URL is required.");
        }

        var currentParent = relations?.FirstOrDefault(relation =>
            string.Equals(relation.Rel, ParentRelationType, StringComparison.OrdinalIgnoreCase));

        if (currentParent is null)
        {
            return true;
        }

        if (string.Equals(
            currentParent.Url?.TrimEnd('/'),
            parentUrl.TrimEnd('/'),
            StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        throw new ArgumentException("The work item already has a different parent. Remove or replace that relation explicitly before assigning a new parent.");
    }

    public static JsonPatchDocument ToPatchDocument(
        IReadOnlyDictionary<string, object?> fields,
        int? rev = null,
        string? parentUrl = null)
    {
        var patch = new JsonPatchDocument();

        if (rev.HasValue)
        {
            patch.Add(new JsonPatchOperation
            {
                Operation = Operation.Test,
                Path = "/rev",
                Value = rev.Value
            });
        }

        foreach (var field in fields)
        {
            patch.Add(new JsonPatchOperation
            {
                Operation = Operation.Add,
                Path = "/fields/" + field.Key,
                Value = field.Value
            });
        }

        if (!string.IsNullOrWhiteSpace(parentUrl))
        {
            patch.Add(new JsonPatchOperation
            {
                Operation = Operation.Add,
                Path = "/relations/-",
                Value = new WorkItemRelation
                {
                    Rel = ParentRelationType,
                    Url = parentUrl
                }
            });
        }

        return patch;
    }

    public static object ToMutationResponse(string operation, string project, string requestedType, string resolvedType, bool validateOnly, WorkItem workItem, IReadOnlyList<string>? warnings = null)
    {
        var fields = workItem.Fields ?? new Dictionary<string, object>();

        return new
        {
            Operation = operation,
            Project = project,
            RequestedType = requestedType,
            ResolvedType = resolvedType,
            ValidateOnly = validateOnly,
            Warnings = warnings ?? [],
            WorkItem = new
            {
                workItem.Id,
                workItem.Url,
                workItem.Rev,
                Title = fields.TryGetValue("System.Title", out var title) ? title : null,
                WorkItemType = fields.TryGetValue("System.WorkItemType", out var type) ? type : null,
                State = fields.TryGetValue("System.State", out var state) ? state : null
            }
        };
    }

    public static object ToUpdateResponse(string project, int id, bool validateOnly, int? rev, WorkItem workItem)
    {
        var fields = workItem.Fields ?? new Dictionary<string, object>();

        return new
        {
            Operation = "update",
            Project = project,
            WorkItemId = id,
            ValidateOnly = validateOnly,
            RevisionGuard = rev,
            WorkItem = new
            {
                workItem.Id,
                workItem.Url,
                workItem.Rev,
                Title = fields.TryGetValue("System.Title", out var title) ? title : null,
                WorkItemType = fields.TryGetValue("System.WorkItemType", out var type) ? type : null,
                State = fields.TryGetValue("System.State", out var state) ? state : null
            }
        };
    }

    public static string BuildMutationFailureMessage(string operation, string project, string? requestedType = null, int? workItemId = null)
    {
        if (string.Equals(operation, "create", StringComparison.OrdinalIgnoreCase))
        {
            return $"Unable to create a work item in project '{project}' using requested type '{requestedType}'. Verify the type exists for the project's process, that required fields such as System.Title are provided, and that the PAT has work item write permission.";
        }

        return $"Unable to update work item '{workItemId}' in project '{project}'. Verify the work item exists, the provided revision guard is current, the updated fields are valid, and the PAT has work item write permission.";
    }

    private static object? ConvertJsonElement(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number when element.TryGetInt64(out var intValue) => intValue,
        JsonValueKind.Number when element.TryGetDecimal(out var decimalValue) => decimalValue,
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null,
        JsonValueKind.Array => element.EnumerateArray().Select(ConvertJsonElement).ToList(),
        JsonValueKind.Object => element.EnumerateObject().ToDictionary(property => property.Name, property => ConvertJsonElement(property.Value), StringComparer.OrdinalIgnoreCase),
        _ => element.ToString()
    };
}
