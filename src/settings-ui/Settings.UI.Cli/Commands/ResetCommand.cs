// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.CommandLine;
using PowerToys.Settings.Cli.Helpers;

namespace PowerToys.Settings.Cli.Commands;

internal sealed class ResetCommand : Command
{
    public ResetCommand()
        : base("reset", "Reset a module's settings (or 'all' for all settings) to defaults")
    {
        var moduleArg = new Argument<string>("module", "Module name (e.g. FancyZones, AlwaysOnTop, or 'all')");

        AddArgument(moduleArg);

        this.SetHandler(
            (string module) =>
            {
                var exitCode = Execute(module);
                Environment.ExitCode = exitCode;
            },
            moduleArg);
    }

    private static int Execute(string module)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(module))
            {
                Console.Error.WriteLine("Module argument is required.");
                return 1;
            }

            SettingsCliHelper.ResetModuleSettings(module);
            Console.WriteLine($"Settings for '{module}' have been reset to defaults.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to reset settings for '{module}': {ex.Message}");
            return 1;
        }
    }
}
