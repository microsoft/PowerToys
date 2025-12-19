// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.CommandLine;
using System.Linq;
using System.Threading.Tasks;
using FancyZonesCLI.CommandLine;

namespace FancyZonesCLI;

internal sealed class Program
{
    private static async Task<int> Main(string[] args)
    {
        Logger.InitializeLogger();
        Logger.LogInfo($"CLI invoked with args: [{string.Join(", ", args)}]");

        // Initialize Windows messages used to notify FancyZones.
        NativeMethods.InitializeWindowMessages();

        // Intercept help requests early and print custom usage.
        if (args.Any(a => string.Equals(a, "--help", StringComparison.OrdinalIgnoreCase) ||
                          string.Equals(a, "-h", StringComparison.OrdinalIgnoreCase) ||
                          string.Equals(a, "-?", StringComparison.OrdinalIgnoreCase)))
        {
            FancyZonesCliUsage.PrintUsage();
            return 0;
        }

        RootCommand rootCommand = FancyZonesCliCommandFactory.CreateRootCommand();
        int exitCode = await rootCommand.InvokeAsync(args);

        if (exitCode == 0)
        {
            Logger.LogInfo("Command completed successfully");
        }
        else
        {
            Logger.LogWarning($"Command failed with exit code {exitCode}");
        }

        return exitCode;
    }
}
