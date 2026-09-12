// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Threading.Tasks;
using ManagedCommon;
using PowerToys.Settings.Cli.Commands;

namespace PowerToys.Settings.Cli;

internal static class Program
{
    internal static RootCommand CreateRootCommand()
    {
        var rootCommand = new RootCommand("PowerToys Settings CLI - Command line interface for PowerToys Settings");

        rootCommand.AddCommand(new ListCommand());
        rootCommand.AddCommand(new GetCommand());
        rootCommand.AddCommand(new SetCommand());
        rootCommand.AddCommand(new ToggleCommand());

        return rootCommand;
    }

    public static async Task<int> Main(string[] args)
    {
        try
        {
            try
            {
                Logger.InitializeLogger("\\Settings\\Cli\\Logs");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Warning: Settings CLI logging is unavailable: {ex.Message}");
            }

            var rootCommand = CreateRootCommand();
            var parser = new Parser(rootCommand);
            var parseResult = parser.Parse(args);
            var exitCode = await rootCommand.InvokeAsync(args);

            if (parseResult.Errors.Count > 0 || exitCode != 0)
            {
                Logger.LogWarning($"Settings CLI command failed with exit code {exitCode}: [{string.Join(", ", args)}]");
            }
            else
            {
                Logger.LogInfo($"Settings CLI command completed: [{string.Join(", ", args)}]");
            }

            return exitCode;
        }
        catch (Exception ex)
        {
            Logger.LogError("Unhandled Settings CLI exception.", ex);
            Console.Error.WriteLine($"Settings CLI failed: {ex.Message}");
            return 1;
        }
    }
}
