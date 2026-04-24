using AzDevOps.McpServer;
using Microsoft.Extensions.Configuration;

namespace AzDevOpsMcp.Tests;

public class DevOpsProjectContextTests
{
    [Fact]
    public void DefaultProject_PrefersAzdoProjectEnvironmentKey()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["AZDO_PROJECT"] = "EnvProject",
            ["project"] = "CommandLineProject"
        });

        var context = new DevOpsProjectContext(configuration);

        Assert.Equal("EnvProject", context.DefaultProject);
    }

    [Fact]
    public void DefaultProject_FallsBackToCommandLineProjectKey()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["project"] = "CommandLineProject"
        });

        var context = new DevOpsProjectContext(configuration);

        Assert.Equal("CommandLineProject", context.DefaultProject);
    }

    [Fact]
    public void DefaultProject_TrimsWhitespace_AndIgnoresEmptyValues()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["AZDO_PROJECT"] = "   ",
            ["project"] = "  WAPP  "
        });

        var context = new DevOpsProjectContext(configuration);

        Assert.Equal("WAPP", context.DefaultProject);
    }

    private static IConfiguration BuildConfiguration(Dictionary<string, string?> values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
}
