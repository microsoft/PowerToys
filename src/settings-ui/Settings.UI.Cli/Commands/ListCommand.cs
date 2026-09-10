// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.CommandLine;
using System.Linq;
using PowerToys.Settings.Cli.Helpers;

namespace PowerToys.Settings.Cli.Commands;

internal sealed class ListCommand : Command
{
    public ListCommand()
        : base("list", "List all PowerToys modules and their status, or list setting properties for a specific module")
    {
        var moduleArg = new Argument<string?>("module", () => null, "Optional module name (e.g. FancyZones, AlwaysOnTop)");
        var jsonOpt = new Option<bool>("--json", "Format output as JSON");

        AddArgument(moduleArg);
        AddOption(jsonOpt);

        this.SetHandler(
            (string? module, bool json) =>
            {
                var exitCode = Execute(module, json);
                Environment.ExitCode = exitCode;
            },
            moduleArg,
            jsonOpt);
    }

    private static int Execute(string? module, bool json)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(module))
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
            }
            else
            {
                var settings = SettingsCliHelper.GetModuleSettings(module);
                if (settings.Count == 0)
                {
                    Console.Error.WriteLine($"Module '{module}' was not found or has no exposed settings.");
                    return 1;
                }

                if (json)
                {
                    Console.WriteLine(SettingsCliHelper.SerializeToJson(settings));
                }
                else
                {
                    Console.WriteLine($"Settings for module '{module}':");
                    Console.WriteLine("-----------------------------");
                    foreach (var (prop, val) in settings.OrderBy(x => x.Key))
                    {
                        Console.WriteLine($"  {prop,-35}: {val}");
                    }
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
