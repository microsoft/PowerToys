// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.CommandLine;
using System.Linq;

namespace FancyZonesCLI.CommandLine;

internal static class FancyZonesCliUsage
{
    public static void PrintUsage()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("FancyZones CLI - Command line interface for FancyZones");
        Console.WriteLine();

        var cmd = FancyZonesCliCommandFactory.CreateRootCommand();

        Console.WriteLine("Usage: FancyZonesCLI [command] [options]");
        Console.WriteLine();

        Console.WriteLine("Options:");
        foreach (var option in cmd.Options)
        {
            var aliases = string.Join(", ", option.Aliases);
            var description = option.Description ?? string.Empty;
            Console.WriteLine($"  {aliases,-30} {description}");
        }

        Console.WriteLine();
        Console.WriteLine("Commands:");
        foreach (var command in cmd.Subcommands)
        {
            if (command.IsHidden)
            {
                continue;
            }

            // Format: "command-name <args>, alias"
            string argsLabel = string.Join(" ", command.Arguments.Select(a => $"<{a.Name}>"));
            string baseLabel = string.IsNullOrEmpty(argsLabel) ? command.Name : $"{command.Name} {argsLabel}";

            // Find first alias (Aliases includes Name)
            string alias = command.Aliases.FirstOrDefault(a => !string.Equals(a, command.Name, StringComparison.OrdinalIgnoreCase));
            string label = string.IsNullOrEmpty(alias) ? baseLabel : $"{baseLabel}, {alias}";

            var description = command.Description ?? string.Empty;
            Console.WriteLine($"  {label,-30} {description}");
        }

        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  FancyZonesCLI --help");
        Console.WriteLine("  FancyZonesCLI --version");
        Console.WriteLine("  FancyZonesCLI get-monitors");
        Console.WriteLine("  FancyZonesCLI set-layout focus");
        Console.WriteLine("  FancyZonesCLI set-layout <uuid> --monitor 1");
        Console.WriteLine("  FancyZonesCLI get-hotkeys");
    }
}
