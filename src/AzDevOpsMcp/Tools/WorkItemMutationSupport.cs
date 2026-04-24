using System.Text.Json;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.WebApi.Patch;
using Microsoft.VisualStudio.Services.WebApi.Patch.Json;

namespace AzDevOps.McpServer;

internal static class WorkItemMutationSupport
{
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

    public static IReadOnlyDictionary<string, object?> BuildCreateFields(string title, string? description, string? fieldsJson)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Title is required to create a work item.");
        }

        var fields = new Dictionary<string, object?>(ParseFieldsJson(fieldsJson), StringComparer.OrdinalIgnoreCase)
        {
            ["System.Title"] = title.Trim()
        };

        if (!string.IsNullOrWhiteSpace(description))
        {
            fields["System.Description"] = description.Trim();
        }

        return fields;
    }

    public static IReadOnlyDictionary<string, object?> BuildUpdateFields(string? title, string? description, string? history, string? fieldsJson)
    {
        var fields = new Dictionary<string, object?>(ParseFieldsJson(fieldsJson), StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(title))
        {
            fields["System.Title"] = title.Trim();
        }

        if (!string.IsNullOrWhiteSpace(description))
        {
            fields["System.Description"] = description.Trim();
        }

        if (!string.IsNullOrWhiteSpace(history))
        {
            fields["System.History"] = history.Trim();
        }

        if (fields.Count == 0)
        {
            throw new ArgumentException("At least one field change is required to update a work item.");
        }

        return fields;
    }

    public static JsonPatchDocument ToPatchDocument(IReadOnlyDictionary<string, object?> fields, int? rev = null)
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

    public static IReadOnlyDictionary<string, object?> ParseFieldsJson(string? fieldsJson)
    {
        if (string.IsNullOrWhiteSpace(fieldsJson))
        {
            return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            using var document = JsonDocument.Parse(fieldsJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new ArgumentException("fieldsJson must be a JSON object with field reference names as keys.");
            }

            return document.RootElement
                .EnumerateObject()
                .ToDictionary(
                    property => property.Name,
                    property => ConvertJsonElement(property.Value),
                    StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            throw new ArgumentException("fieldsJson must be valid JSON.");
        }
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
