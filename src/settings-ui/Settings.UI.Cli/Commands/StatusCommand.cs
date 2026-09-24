// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.CommandLine;
using PowerToys.Settings.Cli.Helpers;

namespace PowerToys.Settings.Cli.Commands;

internal sealed class StatusCommand : Command
{
    public StatusCommand()
        : base("status", "Show one PowerToys module's enabled status")
    {
        var moduleArg = new Argument<string>("module", "Module name (e.g. AlwaysOnTop, FancyZones)");
        var jsonOpt = new Option<bool>("--json", "Format output as JSON");

        AddArgument(moduleArg);
        AddOption(jsonOpt);

        this.SetHandler(context =>
        {
            var module = context.ParseResult.GetValueForArgument(moduleArg);
            var json = context.ParseResult.GetValueForOption(jsonOpt);
            context.ExitCode = Execute(module, json);
        });
    }

    private static int Execute(string module, bool json)
    {
        try
        {
            var moduleStatus = SettingsCliHelper.GetModuleStatus(module);
            if (json)
            {
                Console.WriteLine(SettingsCliHelper.SerializeToJson(moduleStatus));
            }
            else
            {
                var statusStr = moduleStatus.Enabled ? "Enabled" : "Disabled";
                if (moduleStatus.GroupPolicy == "Enabled")
                {
                    statusStr += " [GPO: Enabled]";
                }
                else if (moduleStatus.GroupPolicy == "Disabled")
                {
                    statusStr += " [GPO: Disabled]";
                }

                Console.WriteLine($"Module '{moduleStatus.ModuleName}' is {statusStr}.");
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to get module status for '{module}': {ex.Message}");
            return 1;
        }
    }
}
