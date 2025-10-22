// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using PowerToys.McpServer.Tools;

namespace PowerToys.McpServer
{
    internal sealed class Program
    {
        private static async Task<int> Main(string[] args)
        {
            // Initialize PowerToys logger
            // Logger.InitializeLogger expects path relative to Constants.AppDataPath()
            // which already points to LocalAppData\Microsoft\PowerToys
            string logPath = Path.Combine("\\McpServer", "Logs");
            Logger.InitializeLogger(logPath);
            Logger.LogInfo("Starting PowerToys MCP Server with official SDK");

            try
            {
                var builder = Host.CreateApplicationBuilder(args);

                // Configure all logs to go to stderr (required for MCP protocol)
                builder.Logging.AddConsole(consoleLogOptions =>
                {
                    consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
                });

                // Register MCP server with stdio transport and tools
                builder.Services
                    .AddMcpServer()
                    .WithStdioServerTransport()
                    .WithToolsFromAssembly();

                Logger.LogInfo("Building and running MCP host...");
                await builder.Build().RunAsync();
                Logger.LogInfo("MCP server shutdown complete");

                return 0;
            }
            catch (Exception ex)
            {
                Logger.LogError("Fatal error in MCP server", ex);
                return 1;
            }
        }
    }
}
