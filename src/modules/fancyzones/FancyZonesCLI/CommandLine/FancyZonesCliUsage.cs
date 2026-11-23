// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.CommandLine;
using System.CommandLine.Help;
using System.Globalization;
using System.Linq;

namespace FancyZonesCLI.CommandLine;

internal static class FancyZonesCliUsage
{
    // The name installed on PATH (bin\PowerToys.FancyZones.CLI.exe), not the FancyZonesCLI.exe payload it forwards to.
    private const string CommandName = "PowerToys.FancyZones.CLI.exe";

    public static void PrintUsage()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine(Properties.Resources.usage_title);
        Console.WriteLine();

        var cmd = FancyZonesCliCommandFactory.CreateRootCommand();

        Console.WriteLine(Properties.Resources.usage_syntax);
        Console.WriteLine();

        Console.WriteLine(Properties.Resources.usage_options);

        // Skip RootCommand's built-in --help/--version; the examples below cover them.
        foreach (var option in cmd.Options.Where(o => o is not HelpOption and not VersionOption))
        {
            var aliases = string.Join(", ", option.Aliases.Prepend(option.Name));
            var description = option.Description ?? string.Empty;
            Console.WriteLine($"  {aliases,-30} {description}");
        }

        Console.WriteLine();
        Console.WriteLine(Properties.Resources.usage_commands);
        foreach (var command in cmd.Subcommands)
        {
            if (command.Hidden)
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
        Console.WriteLine(Properties.Resources.usage_examples);
        Console.WriteLine($"  {CommandName} --help");
        Console.WriteLine($"  {CommandName} --version");
        Console.WriteLine($"  {CommandName} get-monitors");
        Console.WriteLine($"  {CommandName} set-layout focus");
        Console.WriteLine($"  {CommandName} set-layout <uuid> --monitor 1");
        Console.WriteLine($"  {CommandName} get-hotkeys");
    }

    public static void PrintCommandUsage(string commandName)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        var rootCmd = FancyZonesCliCommandFactory.CreateRootCommand();

        // Find matching subcommand by name or alias
        var subcommand = rootCmd.Subcommands.FirstOrDefault(c =>
            string.Equals(c.Name, commandName, StringComparison.OrdinalIgnoreCase) ||
            c.Aliases.Any(a => string.Equals(a, commandName, StringComparison.OrdinalIgnoreCase)));

        if (subcommand == null)
        {
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture, Properties.Resources.usage_unknown_command, commandName));
            Console.WriteLine();
            Console.WriteLine(Properties.Resources.usage_run_help);
            return;
        }

        // Command name and description
        Console.WriteLine($"{Properties.Resources.usage_command} {subcommand.Name}");
        if (!string.IsNullOrEmpty(subcommand.Description))
        {
            Console.WriteLine($"  {subcommand.Description}");
        }

        Console.WriteLine();

        // Usage line
        string argsLabel = string.Join(" ", subcommand.Arguments.Select(a => $"<{a.Name}>"));
        string optionsLabel = subcommand.Options.Count > 0 ? " [options]" : string.Empty;
        Console.WriteLine($"Usage: {CommandName} {subcommand.Name} {argsLabel}{optionsLabel}".TrimEnd());
        Console.WriteLine();

        // Aliases
        var aliases = subcommand.Aliases.Where(a => !string.Equals(a, subcommand.Name, StringComparison.OrdinalIgnoreCase)).ToList();
        if (aliases.Count > 0)
        {
            Console.WriteLine($"{Properties.Resources.usage_aliases} {string.Join(", ", aliases)}");
            Console.WriteLine();
        }

        // Arguments
        if (subcommand.Arguments.Count > 0)
        {
            Console.WriteLine(Properties.Resources.usage_arguments);
            foreach (var arg in subcommand.Arguments)
            {
                var argDescription = arg.Description ?? string.Empty;
                Console.WriteLine($"  <{arg.Name}>{(arg.Arity.MinimumNumberOfValues == 0 ? $" {Properties.Resources.usage_optional}" : string.Empty),-20} {argDescription}");
            }

            Console.WriteLine();
        }

        // Options
        if (subcommand.Options.Count > 0)
        {
            Console.WriteLine(Properties.Resources.usage_options);
            foreach (var option in subcommand.Options)
            {
                var optAliases = string.Join(", ", option.Aliases.Prepend(option.Name));
                var optDescription = option.Description ?? string.Empty;
                Console.WriteLine($"  {optAliases,-25} {optDescription}");
            }

            Console.WriteLine();
        }

        // Command-specific examples
        PrintCommandExamples(subcommand.Name);
    }

    private static void PrintCommandExamples(string commandName)
    {
        Console.WriteLine(Properties.Resources.usage_examples);

        switch (commandName.ToLowerInvariant())
        {
            case "get-monitors":
                Console.WriteLine($"  {CommandName} get-monitors");
                Console.WriteLine($"  {CommandName} m");
                break;

            case "get-layouts":
                Console.WriteLine($"  {CommandName} get-layouts");
                Console.WriteLine($"  {CommandName} ls");
                break;

            case "get-active-layout":
                Console.WriteLine($"  {CommandName} get-active-layout");
                Console.WriteLine($"  {CommandName} active");
                break;

            case "set-layout":
                Console.WriteLine($"  {CommandName} set-layout focus");
                Console.WriteLine($"  {CommandName} set-layout columns --monitor 1");
                Console.WriteLine($"  {CommandName} set-layout {{uuid}} --all");
                Console.WriteLine($"  {CommandName} s rows -m 2");
                break;

            case "open-editor":
                Console.WriteLine($"  {CommandName} open-editor");
                Console.WriteLine($"  {CommandName} e");
                break;

            case "open-settings":
                Console.WriteLine($"  {CommandName} open-settings");
                Console.WriteLine($"  {CommandName} settings");
                break;

            case "get-hotkeys":
                Console.WriteLine($"  {CommandName} get-hotkeys");
                Console.WriteLine($"  {CommandName} hk");
                break;

            case "set-hotkey":
                Console.WriteLine($"  {CommandName} set-hotkey 1 {{layout-uuid}}");
                Console.WriteLine($"  {CommandName} shk 2 0CEBCBA9-9C32-4395-B93E-DC77485AD6D0");
                break;

            case "remove-hotkey":
                Console.WriteLine($"  {CommandName} remove-hotkey 1");
                Console.WriteLine($"  {CommandName} rhk 2");
                break;

            default:
                Console.WriteLine($"  {CommandName} {commandName}");
                break;
        }
    }
}
