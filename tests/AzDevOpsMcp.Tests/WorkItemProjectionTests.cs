using AzDevOps.McpServer;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;

namespace AzDevOpsMcp.Tests;

public class WorkItemProjectionTests
{
    [Fact]
    public void ResolveFields_IncludesCompactDefaults_AndRequestedFields()
    {
        var fields = WorkItemProjection.ResolveFields("System.Tags,System.CreatedDate,System.Tags");

        Assert.Contains("System.Title", fields);
        Assert.Contains("System.Tags", fields);
        Assert.Contains("System.CreatedDate", fields);
        Assert.Equal(fields.Count, fields.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void Project_ReturnsCompactShape_WithNormalizedFields()
    {
        var workItem = new WorkItem
        {
            Id = 42,
            Url = "https://dev.azure.com/example/_apis/wit/workItems/42",
            Rev = 3,
            Fields = new Dictionary<string, object>
            {
                ["System.Title"] = "Compact result",
                ["System.WorkItemType"] = "User Story",
                ["System.State"] = "Active",
                ["System.AssignedTo"] = new Dictionary<string, object>
                {
                    ["displayName"] = "Hudson Medeiros",
                    ["uniqueName"] = "medeiroshudson@outlook.com"
                },
                ["System.ChangedDate"] = new DateTime(2026, 4, 16, 0, 0, 0, DateTimeKind.Utc),
                ["System.Tags"] = "alpha; beta; alpha",
                ["Custom.Field"] = "extra"
            }
        };

        var projected = (Dictionary<string, object?>)WorkItemProjection.Project(
            workItem,
            [
                "System.Id",
                "System.WorkItemType",
                "System.Title",
                "System.State",
                "System.AssignedTo",
                "System.ChangedDate",
                "System.Tags",
                "Custom.Field"
            ],
            includeRelations: false);

        Assert.Equal(42, projected["id"]);
        Assert.Equal("Compact result", projected["title"]);
        Assert.Equal("User Story", projected["workItemType"]);
        Assert.Equal("Active", projected["state"]);

        var assignedTo = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(projected["assignedTo"]);
        Assert.Equal("Hudson Medeiros", assignedTo["displayName"]);
        Assert.Equal("medeiroshudson@outlook.com", assignedTo["uniqueName"]);

        var tags = Assert.IsType<List<string>>(projected["tags"]);
        Assert.Equal(["alpha", "beta"], tags);

        var additionalFields = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(projected["additionalFields"]);
        Assert.Equal("extra", additionalFields["Custom.Field"]);
    }
}
