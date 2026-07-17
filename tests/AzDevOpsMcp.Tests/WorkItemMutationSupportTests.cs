using AzDevOps.McpServer;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.WebApi.Patch;
using Microsoft.VisualStudio.Services.WebApi.Patch.Json;
using System.Text.Json;

namespace AzDevOpsMcp.Tests;

public class WorkItemMutationSupportTests
{
    [Fact]
    public void ResolveTypeName_ReturnsExactMatch_WhenAvailable()
    {
        var resolved = WorkItemMutationSupport.ResolveTypeName("Task", ["Bug", "Task", "Issue"], out var warning);

        Assert.Equal("Task", resolved);
        Assert.Null(warning);
    }

    [Fact]
    public void ResolveTypeName_MapsGenericUserStoryAlias_ToProjectSpecificRequirementType()
    {
        var resolved = WorkItemMutationSupport.ResolveTypeName("User Story", ["Issue", "Task"], out var warning);

        Assert.Equal("Issue", resolved);
        Assert.Equal("Requested type 'User Story' was resolved to 'Issue' for the target project's process.", warning);
    }

    [Fact]
    public void BuildFields_ConvertsCreateFields()
    {
        var input = ParseFields("""
            {
              "System.Title": "My title",
              "System.Description": "My description",
              "Microsoft.VSTS.Common.Priority": 1
            }
            """);

        var fields = WorkItemMutationSupport.BuildFields(input);

        Assert.Equal("My title", fields["System.Title"]);
        Assert.Equal("My description", fields["System.Description"]);
        Assert.Equal(1L, fields["Microsoft.VSTS.Common.Priority"]);
    }

    [Fact]
    public void BuildFields_AddsTrimmedHistoryToUpdateFields()
    {
        var input = ParseFields("""{ "System.State": "Active" }""");

        var fields = WorkItemMutationSupport.BuildFields(input, " Updated via MCP ");

        Assert.Equal("Active", fields["System.State"]);
        Assert.Equal("Updated via MCP", fields["System.History"]);
    }

    [Fact]
    public void BuildFields_ConvertsNestedValues()
    {
        var input = ParseFields("""
            {
              "Custom.String": "value",
              "Custom.Decimal": 1.5,
              "Custom.Boolean": true,
              "Custom.Null": null,
              "Custom.Array": ["one", 2],
              "Custom.Object": { "name": "child" }
            }
            """);

        var fields = WorkItemMutationSupport.BuildFields(input);

        Assert.Equal("value", fields["Custom.String"]);
        Assert.Equal(1.5m, fields["Custom.Decimal"]);
        Assert.Equal(true, fields["Custom.Boolean"]);
        Assert.Null(fields["Custom.Null"]);

        var array = Assert.IsType<List<object?>>(fields["Custom.Array"]);
        Assert.Equal(["one", 2L], array);

        var nestedObject = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(fields["Custom.Object"]);
        Assert.Equal("child", nestedObject["name"]);
    }

    [Fact]
    public void BuildFields_AllowsHistoryOnly()
    {
        var fields = WorkItemMutationSupport.BuildFields(null, " Updated via MCP ");

        Assert.Single(fields);
        Assert.Equal("Updated via MCP", fields["System.History"]);
    }

    [Fact]
    public void BuildFields_RequiresAtLeastOneField()
    {
        var error = Assert.Throws<ArgumentException>(() => WorkItemMutationSupport.BuildFields(null));

        Assert.Equal(
            "At least one field is required. Pass a JSON object with field reference names as keys, e.g. {\"System.Title\":\"My title\"}.",
            error.Message);
    }

    [Fact]
    public void BuildFields_AllowsEmptyFieldsForParentOnlyUpdate()
    {
        var fields = WorkItemMutationSupport.BuildFields(null, allowEmpty: true);

        Assert.Empty(fields);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ValidateParentId_RejectsNonPositiveId(int parentId)
    {
        var error = Assert.Throws<ArgumentException>(() => WorkItemMutationSupport.ValidateParentId(parentId));

        Assert.Equal("parentId must be greater than zero.", error.Message);
    }

    [Fact]
    public void ValidateParentId_RejectsSelfParentOnUpdate()
    {
        var error = Assert.Throws<ArgumentException>(() => WorkItemMutationSupport.ValidateParentId(10, childId: 10));

        Assert.Equal("A work item cannot be its own parent.", error.Message);
    }

    [Fact]
    public void ValidateRevision_RejectsStaleRevision()
    {
        var error = Assert.Throws<ArgumentException>(() => WorkItemMutationSupport.ValidateRevision(4, 5));

        Assert.Equal("Revision guard '4' does not match the current revision '5'.", error.Message);
    }

    [Fact]
    public void ToPatchDocument_IncludesRevisionGuard_WhenProvided()
    {
        var patch = WorkItemMutationSupport.ToPatchDocument(
            new Dictionary<string, object?>
            {
                ["System.Title"] = "Updated title"
            },
            rev: 12);

        Assert.Collection(
            patch,
            operation =>
            {
                Assert.Equal(Operation.Test, operation.Operation);
                Assert.Equal("/rev", operation.Path);
                Assert.Equal(12, operation.Value);
            },
            operation =>
            {
                Assert.Equal(Operation.Add, operation.Operation);
                Assert.Equal("/fields/System.Title", operation.Path);
                Assert.Equal("Updated title", operation.Value);
            });
    }

    [Fact]
    public void ToPatchDocument_AppendsParentRelationAfterRevisionAndFields()
    {
        const string parentUrl = "https://dev.azure.com/example/_apis/wit/workItems/10";

        var patch = WorkItemMutationSupport.ToPatchDocument(
            new Dictionary<string, object?> { ["System.Title"] = "Updated title" },
            rev: 12,
            parentUrl: parentUrl);

        Assert.Collection(
            patch,
            operation => Assert.Equal(Operation.Test, operation.Operation),
            operation => Assert.Equal("/fields/System.Title", operation.Path),
            operation =>
            {
                Assert.Equal(Operation.Add, operation.Operation);
                Assert.Equal("/relations/-", operation.Path);
                var relation = Assert.IsType<WorkItemRelation>(operation.Value);
                Assert.Equal("System.LinkTypes.Hierarchy-Reverse", relation.Rel);
                Assert.Equal(parentUrl, relation.Url);
            });
    }

    [Fact]
    public void ShouldAddParentRelation_ReturnsTrue_WhenNoParentExists()
    {
        var relations = new List<WorkItemRelation>
        {
            new() { Rel = "System.LinkTypes.Related", Url = "https://dev.azure.com/example/_apis/wit/workItems/20" }
        };

        var shouldAdd = WorkItemMutationSupport.ShouldAddParentRelation(
            relations,
            "https://dev.azure.com/example/_apis/wit/workItems/10");

        Assert.True(shouldAdd);
    }

    [Fact]
    public void ShouldAddParentRelation_ReturnsFalse_WhenSameParentExists()
    {
        const string parentUrl = "https://dev.azure.com/example/_apis/wit/workItems/10";
        var relations = new List<WorkItemRelation>
        {
            new() { Rel = "System.LinkTypes.Hierarchy-Reverse", Url = parentUrl + "/" }
        };

        var shouldAdd = WorkItemMutationSupport.ShouldAddParentRelation(relations, parentUrl);

        Assert.False(shouldAdd);
    }

    [Fact]
    public void ShouldAddParentRelation_RejectsDifferentExistingParent()
    {
        var relations = new List<WorkItemRelation>
        {
            new() { Rel = "System.LinkTypes.Hierarchy-Reverse", Url = "https://dev.azure.com/example/_apis/wit/workItems/20" }
        };

        var error = Assert.Throws<ArgumentException>(() => WorkItemMutationSupport.ShouldAddParentRelation(
            relations,
            "https://dev.azure.com/example/_apis/wit/workItems/10"));

        Assert.Equal(
            "The work item already has a different parent. Remove or replace that relation explicitly before assigning a new parent.",
            error.Message);
    }

    private static Dictionary<string, JsonElement> ParseFields(string json) =>
        JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;
}
