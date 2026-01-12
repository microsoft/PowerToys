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
    private static readonly string[] HelpFlags = ["--help", "-h", "-?"];

    private static async Task<int> Main(string[] args)
    {
        Logger.InitializeLogger();
        Logger.LogInfo($"CLI invoked with args: [{string.Join(", ", args)}]");

        // Initialize Windows messages used to notify FancyZones.
        NativeMethods.InitializeWindowMessages();

        // Intercept help requests early and print custom usage.
        if (TryHandleHelpRequest(args))
        {
            return 0;
        }

        // Detect PowerShell script block expansion (when {} is interpreted as script block)
        if (DetectPowerShellScriptBlockArgs(args))
        {
            return 1;
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

    /// <summary>
    /// Handles help requests for root command and subcommands.
    /// </summary>
    /// <returns>True if help was printed, false otherwise.</returns>
    private static bool TryHandleHelpRequest(string[] args)
    {
        bool hasHelpFlag = args.Any(a => HelpFlags.Any(h => string.Equals(a, h, StringComparison.OrdinalIgnoreCase)));
        if (!hasHelpFlag)
        {
            return false;
        }

        // Get non-help arguments to identify subcommand
        var nonHelpArgs = args.Where(a => !HelpFlags.Any(h => string.Equals(a, h, StringComparison.OrdinalIgnoreCase))).ToArray();

        if (nonHelpArgs.Length == 0)
        {
            // Root help: fancyzonescli --help
            FancyZonesCliUsage.PrintUsage();
        }
        else
        {
            // Subcommand help: fancyzonescli <command> --help
            string subcommandName = nonHelpArgs[0];
            FancyZonesCliUsage.PrintCommandUsage(subcommandName);
        }

        return true;
    }

    /// <summary>
    /// Detects when PowerShell interprets {GUID} as a script block and converts it to encoded command args.
    /// This happens when users forget to quote GUIDs with braces in PowerShell.
    /// </summary>
    /// <returns>True if PowerShell script block args were detected, false otherwise.</returns>
    private static bool DetectPowerShellScriptBlockArgs(string[] args)
    {
        // PowerShell converts {scriptblock} to: -encodedCommand <base64> -inputFormat xml -outputFormat text
        bool hasEncodedCommand = args.Any(a => string.Equals(a, "-encodedCommand", StringComparison.OrdinalIgnoreCase));
        bool hasInputFormat = args.Any(a => string.Equals(a, "-inputFormat", StringComparison.OrdinalIgnoreCase));
        bool hasOutputFormat = args.Any(a => string.Equals(a, "-outputFormat", StringComparison.OrdinalIgnoreCase));

        if (hasEncodedCommand || (hasInputFormat && hasOutputFormat))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(Properties.Resources.error_powershell_scriptblock_title);
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine(Properties.Resources.error_powershell_scriptblock_explanation);
            Console.WriteLine(Properties.Resources.error_powershell_scriptblock_hint);
            Console.WriteLine();
            Console.WriteLine($"  {Properties.Resources.error_powershell_scriptblock_option1}");
            Console.WriteLine($"    {Properties.Resources.error_powershell_scriptblock_option1_example}");
            Console.WriteLine();
            Console.WriteLine($"  {Properties.Resources.error_powershell_scriptblock_option2}");
            Console.WriteLine($"    {Properties.Resources.error_powershell_scriptblock_option2_example}");
            Console.WriteLine();

            Logger.LogWarning("PowerShell script block expansion detected - user needs to quote GUID or omit braces");
            return true;
        }

        return false;
    }
}
