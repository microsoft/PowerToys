// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Linq;
using PowerToys.Settings.Cli.Helpers;

namespace PowerToys.Settings.Cli.Commands;

internal sealed class GetCommand : Command
{
    public GetCommand()
        : base("get", "Get settings for a module or a specific setting value (e.g. 'FancyZones' or 'FancyZones.FancyzonesShiftDrag')")
    {
        var targetArg = new Argument<string>("target", "Module name or setting property in format 'Module.SettingName' or 'Enabled.ModuleName'");
        var jsonOpt = new Option<bool>("--json", "Format output as JSON");

        AddArgument(targetArg);
        AddOption(jsonOpt);

        this.SetHandler(context =>
        {
            var target = context.ParseResult.GetValueForArgument(targetArg);
            var json = context.ParseResult.GetValueForOption(jsonOpt);
            context.ExitCode = Execute(target, json);
        });
    }

    private static int Execute(string target, bool json)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(target))
            {
                Console.Error.WriteLine("Target parameter is required.");
                return 1;
            }

            if (target.Contains('.'))
            {
                var val = SettingsCliHelper.GetSettingValue(target);
                if (json)
                {
                    var result = new Dictionary<string, object?> { [target] = val };
                    Console.WriteLine(SettingsCliHelper.SerializeToJson(result));
                }
                else
                {
                    Console.WriteLine(val?.ToString() ?? "null");
                }

                return 0;
            }
            else
            {
                var settings = SettingsCliHelper.GetModuleSettings(target);
                if (settings.Count == 0)
                {
                    Console.Error.WriteLine($"Module '{target}' was not found or has no exposed settings.");
                    return 1;
                }

                if (json)
                {
                    Console.WriteLine(SettingsCliHelper.SerializeToJson(settings));
                }
                else
                {
                    Console.WriteLine($"Settings for '{target}':");
                    foreach (var (prop, val) in settings.OrderBy(x => x.Key))
                    {
                        Console.WriteLine($"  {prop}: {val}");
                    }
                }

                return 0;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error retrieving setting: {ex.Message}");
            return 1;
        }
    }
}
