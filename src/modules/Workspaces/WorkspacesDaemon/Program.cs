// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PowerToys.WorkspacesMCP.Services;
using PowerToys.WorkspacesMCP.Services.Workspaces;

namespace PowerToys.WorkspacesMCP;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            var host = CreateHost(args);

            using var scope = host.Services.CreateScope();
            var mcpService = scope.ServiceProvider.GetRequiredService<MCPProtocolService>();

            // MCP uses stdin/stdout for communication
            using var cancellationTokenSource = new CancellationTokenSource();

            // Handle Ctrl+C gracefully
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                cancellationTokenSource.Cancel();
            };

            // Process MCP messages
            await mcpService.ProcessMessagesAsync(
                Console.OpenStandardInput(),
                Console.OpenStandardOutput(),
                cancellationTokenSource.Token);

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Unhandled exception: {ex}");
            return 1;
        }
    }

    private static IHost CreateHost(string[] args)
    {
        return Host.CreateDefaultBuilder(args)
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
            })
            .ConfigureServices(services =>
            {
                // Windows API service (fallback enumeration)
                services.AddSingleton<WindowsApiService>();

                // Workspace state + monitor
                services.AddSingleton<WorkspaceStateProvider>();
                services.AddSingleton<IWorkspaceStateProvider>(sp => sp.GetRequiredService<WorkspaceStateProvider>());
                services.AddHostedService<WorkspaceMonitor>();

                // Workspace catalog
                services.AddSingleton<IWorkspaceCatalog, WorkspaceCatalogService>();

                // MCP protocol handler (uses state provider)
                services.AddSingleton<MCPProtocolService>();
            })
            .Build();
    }
}
