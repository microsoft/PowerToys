// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using PowerToys.Settings.Cli.Helpers;

namespace PowerToys.Settings.Cli.Commands;

internal sealed class ToggleCommand : Command
{
    public ToggleCommand()
        : base("toggle", "Toggle a module's enabled status")
    {
        var moduleArg = new Argument<string>("module", "Module name (e.g. AlwaysOnTop, FancyZones)");
        var enableOpt = new Option<bool>("--enable", "Explicitly enable the module");
        var disableOpt = new Option<bool>("--disable", "Explicitly disable the module");

        AddArgument(moduleArg);
        AddOption(enableOpt);
        AddOption(disableOpt);

        this.SetHandler(context =>
        {
            var module = context.ParseResult.GetValueForArgument(moduleArg);
            var enable = context.ParseResult.GetValueForOption(enableOpt);
            var disable = context.ParseResult.GetValueForOption(disableOpt);
            context.ExitCode = Execute(module, enable, disable);
        });
    }

    private static int Execute(string module, bool enable, bool disable)
    {
        try
        {
            if (enable && disable)
            {
                Console.Error.WriteLine("Cannot specify both --enable and --disable.");
                return 1;
            }

            bool? targetState = null;
            if (enable)
            {
                targetState = true;
            }

            if (disable)
            {
                targetState = false;
            }

            var newState = SettingsCliHelper.ToggleModule(module, targetState);
            var statusStr = newState ? "Enabled" : "Disabled";
            Console.WriteLine($"Module '{module}' is saved as {statusStr}. Restart PowerToys to apply the module state.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to toggle module '{module}': {ex.Message}");
            return 1;
        }
    }
}
