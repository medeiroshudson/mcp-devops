using AzDevOps.McpServer;
using Microsoft.VisualStudio.Services.WebApi.Patch;
using Microsoft.VisualStudio.Services.WebApi.Patch.Json;

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
    public void BuildCreateFields_RequiresTitle_AndMergesJsonFields()
    {
        var fields = WorkItemMutationSupport.BuildCreateFields(
            "My title",
            "My description",
            "{\"Microsoft.VSTS.Common.Priority\":1}");

        Assert.Equal("My title", fields["System.Title"]);
        Assert.Equal("My description", fields["System.Description"]);
        Assert.Equal(1L, fields["Microsoft.VSTS.Common.Priority"]);
    }

    [Fact]
    public void BuildUpdateFields_RequiresAtLeastOneChange()
    {
        var error = Assert.Throws<ArgumentException>(() => WorkItemMutationSupport.BuildUpdateFields(null, null, null, null));

        Assert.Equal("At least one field change is required to update a work item.", error.Message);
    }

    [Fact]
    public void ParseFieldsJson_RejectsNonObjectPayloads()
    {
        var error = Assert.Throws<ArgumentException>(() => WorkItemMutationSupport.ParseFieldsJson("[1,2,3]"));

        Assert.Equal("fieldsJson must be a JSON object with field reference names as keys.", error.Message);
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
}
