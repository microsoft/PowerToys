// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Linq;
using PowerToys.Settings.Cli.Helpers;

namespace PowerToys.Settings.Cli.Commands;

internal sealed class ListCommand : Command
{
    public ListCommand()
        : base("list", "List all PowerToys modules and their enabled status")
    {
        var jsonOpt = new Option<bool>("--json", "Format output as JSON");

        AddOption(jsonOpt);

        this.SetHandler(context =>
        {
            var json = context.ParseResult.GetValueForOption(jsonOpt);
            context.ExitCode = Execute(json);
        });
    }

    private static int Execute(bool json)
    {
        try
        {
            var modules = SettingsCliHelper.GetModulesAndStatus();
            if (json)
            {
                Console.WriteLine(SettingsCliHelper.SerializeToJson(modules));
            }
            else
            {
                Console.WriteLine("PowerToys Modules Status:");
                Console.WriteLine("-----------------------");
                foreach (var (mod, enabled) in modules.OrderBy(x => x.Key))
                {
                    var statusStr = enabled ? "Enabled" : "Disabled";
                    var gpoRule = SettingsCliHelper.GetModuleGpoRule(mod);
                    if (gpoRule == PowerToys.GPOWrapper.GpoRuleConfigured.Disabled)
                    {
                        statusStr += " [GPO: Disabled]";
                    }
                    else if (gpoRule == PowerToys.GPOWrapper.GpoRuleConfigured.Enabled)
                    {
                        statusStr += " [GPO: Enabled]";
                    }

                    Console.WriteLine($"  {mod,-25}: {statusStr}");
                }
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error executing list command: {ex.Message}");
            return 1;
        }
    }
}
