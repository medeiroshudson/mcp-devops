using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;

namespace AzDevOps.McpServer;

internal static class WorkItemProjection
{
    private static readonly HashSet<string> CompactFieldNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "System.Id",
        "System.WorkItemType",
        "System.Title",
        "System.State",
        "System.AssignedTo",
        "System.ChangedDate"
    };

    public static IReadOnlyList<string> ResolveFields(string? fieldsCsv)
    {
        var requestedFields = string.IsNullOrWhiteSpace(fieldsCsv)
            ? []
            : fieldsCsv.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

        return WorkItemQueryDefaults.DefaultFields
            .Concat(requestedFields)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static object Project(WorkItem workItem, IReadOnlyList<string> requestedFields, bool includeRelations)
    {
        var fields = workItem.Fields ?? new Dictionary<string, object>();
        var projected = new Dictionary<string, object?>
        {
            ["id"] = workItem.Id,
            ["url"] = workItem.Url,
            ["rev"] = workItem.Rev
        };

        AddWhenHasValue(projected, "title", ReadString(fields, "System.Title"));
        AddWhenHasValue(projected, "workItemType", ReadString(fields, "System.WorkItemType"));
        AddWhenHasValue(projected, "state", ReadString(fields, "System.State"));
        AddWhenHasValue(projected, "assignedTo", NormalizeAssignedTo(ReadField(fields, "System.AssignedTo")));
        AddWhenHasValue(projected, "changedDate", NormalizeDate(ReadField(fields, "System.ChangedDate")));

        if (requestedFields.Contains("System.Tags", StringComparer.OrdinalIgnoreCase))
        {
            var tags = NormalizeTags(ReadString(fields, "System.Tags"));
            if (tags.Count > 0)
            {
                projected["tags"] = tags;
            }
        }

        if (requestedFields.Contains("System.CreatedDate", StringComparer.OrdinalIgnoreCase))
        {
            AddWhenHasValue(projected, "createdDate", NormalizeDate(ReadField(fields, "System.CreatedDate")));
        }

        var additionalFields = requestedFields
            .Where(fieldName => !CompactFieldNames.Contains(fieldName) && fieldName is not "System.Tags" and not "System.CreatedDate")
            .Select(fieldName => new KeyValuePair<string, object?>(fieldName, NormalizeFieldValue(ReadField(fields, fieldName))))
            .Where(entry => entry.Value is not null)
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.OrdinalIgnoreCase);

        if (additionalFields.Count > 0)
        {
            projected["additionalFields"] = additionalFields;
        }

        if (includeRelations && workItem.Relations?.Count > 0)
        {
            projected["relations"] = workItem.Relations
                .Select(relation => new Dictionary<string, object?>
                {
                    ["rel"] = relation.Rel,
                    ["url"] = relation.Url,
                    ["attributes"] = NormalizeRelationAttributes(relation.Attributes)
                })
                .ToList();
        }

        return projected;
    }

    private static object? ReadField(IDictionary<string, object> fields, string fieldName) =>
        fields.TryGetValue(fieldName, out var value) ? value : null;

    private static string? ReadString(IDictionary<string, object> fields, string fieldName) =>
        NormalizeFieldValue(ReadField(fields, fieldName)) as string;

    private static IReadOnlyDictionary<string, object?>? NormalizeAssignedTo(object? value)
    {
        if (value is null)
        {
            return null;
        }

        if (value is IDictionary<string, object> dictionary)
        {
            var displayName = dictionary.TryGetValue("displayName", out var displayNameValue)
                ? displayNameValue?.ToString()
                : dictionary.TryGetValue("DisplayName", out var displayNamePascal)
                    ? displayNamePascal?.ToString()
                    : null;
            var uniqueName = dictionary.TryGetValue("uniqueName", out var uniqueNameValue)
                ? uniqueNameValue?.ToString()
                : dictionary.TryGetValue("UniqueName", out var uniqueNamePascal)
                    ? uniqueNamePascal?.ToString()
                    : null;

            if (displayName is null && uniqueName is null)
            {
                return null;
            }

            return new Dictionary<string, object?>
            {
                ["displayName"] = displayName,
                ["uniqueName"] = uniqueName
            };
        }

        if (value is IReadOnlyDictionary<string, object> readOnlyDictionary)
        {
            var displayName = readOnlyDictionary.TryGetValue("displayName", out var displayNameValue)
                ? displayNameValue?.ToString()
                : readOnlyDictionary.TryGetValue("DisplayName", out var displayNamePascal)
                    ? displayNamePascal?.ToString()
                    : null;
            var uniqueName = readOnlyDictionary.TryGetValue("uniqueName", out var uniqueNameValue)
                ? uniqueNameValue?.ToString()
                : readOnlyDictionary.TryGetValue("UniqueName", out var uniqueNamePascal)
                    ? uniqueNamePascal?.ToString()
                    : null;

            if (displayName is null && uniqueName is null)
            {
                return null;
            }

            return new Dictionary<string, object?>
            {
                ["displayName"] = displayName,
                ["uniqueName"] = uniqueName
            };
        }

        var reflectedDisplayName = ReadPropertyValue(value, "DisplayName");
        var reflectedUniqueName = ReadPropertyValue(value, "UniqueName");

        if (reflectedDisplayName is not null || reflectedUniqueName is not null)
        {
            return new Dictionary<string, object?>
            {
                ["displayName"] = reflectedDisplayName,
                ["uniqueName"] = reflectedUniqueName
            };
        }

        return new Dictionary<string, object?>
        {
            ["displayName"] = value.ToString(),
            ["uniqueName"] = null
        };
    }

    private static object? NormalizeFieldValue(object? value) => value switch
    {
        null => null,
        DateTime dateTime => NormalizeDate(dateTime),
        DateTimeOffset dateTimeOffset => NormalizeDate(dateTimeOffset),
        string stringValue => stringValue,
        _ => value
    };

    private static string? NormalizeDate(object? value) => value switch
    {
        null => null,
        DateTime dateTime => dateTime.ToUniversalTime().ToString("O"),
        DateTimeOffset dateTimeOffset => dateTimeOffset.ToUniversalTime().ToString("O"),
        _ => value.ToString()
    };

    private static List<string> NormalizeTags(string? tagsValue) =>
        string.IsNullOrWhiteSpace(tagsValue)
            ? []
            : tagsValue
                .Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

    private static IReadOnlyDictionary<string, object?>? NormalizeRelationAttributes(IDictionary<string, object>? attributes)
    {
        if (attributes is null || attributes.Count == 0)
        {
            return null;
        }

        return attributes.ToDictionary(entry => entry.Key, entry => NormalizeFieldValue(entry.Value), StringComparer.OrdinalIgnoreCase);
    }

    private static void AddWhenHasValue(IDictionary<string, object?> target, string key, object? value)
    {
        if (value is not null)
        {
            target[key] = value;
        }
    }

    private static string? ReadPropertyValue(object value, string propertyName) =>
        value.GetType().GetProperty(propertyName)?.GetValue(value)?.ToString();
}
