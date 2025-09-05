using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AzDevOps.McpServer;

internal class Program
{
    private static async Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        _ = builder.Logging.AddConsole(o =>
        {
            o.LogToStandardErrorThreshold = LogLevel.Trace;
        });

        _ = builder.Services.AddSingleton<Tools>();
        _ = builder.Services.AddSingleton<IDevOpsClientFactory, DevOpsClientFactory>();
        _ = builder.Services.AddSingleton<IDevOpsProjectContext, DevOpsProjectContext>();

        _ = builder.Services
            .AddMcpServer()
            .WithStdioServerTransport()
            .WithToolsFromAssembly();

        var host = builder.Build();

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (s, e) => { e.Cancel = true; cts.Cancel(); };

        try
        {
            await host.RunAsync(cts.Token);
        }
        catch (Exception ex)
        {
            if (host.Services.GetService(typeof(ILogger<Program>)) is ILogger<Program> logger)
            {
                logger.LogCritical(ex, "Unhandled exception during host execution");
            }
            else
            {
                Console.Error.WriteLine($"Unhandled exception: {ex}");
            }

            Environment.ExitCode = 1;
        }
    }
}
