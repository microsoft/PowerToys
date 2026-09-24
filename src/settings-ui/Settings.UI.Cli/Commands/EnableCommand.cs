// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.CommandLine;
using PowerToys.Settings.Cli.Helpers;

namespace PowerToys.Settings.Cli.Commands;

internal sealed class EnableCommand : Command
{
    public EnableCommand()
        : base("enable", "Enable a PowerToys module")
    {
        var moduleArg = new Argument<string>("module", "Module name (e.g. AlwaysOnTop, FancyZones)");
        AddArgument(moduleArg);

        this.SetHandler(context =>
        {
            var module = context.ParseResult.GetValueForArgument(moduleArg);
            context.ExitCode = Execute(module);
        });
    }

    private static int Execute(string module)
    {
        try
        {
            var moduleStatus = SettingsCliHelper.SetModuleEnabled(module, enabled: true);
            Console.WriteLine($"Module '{moduleStatus.ModuleName}' is saved as Enabled. Restart PowerToys to apply the module state.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to enable module '{module}': {ex.Message}");
            return 1;
        }
    }
}
