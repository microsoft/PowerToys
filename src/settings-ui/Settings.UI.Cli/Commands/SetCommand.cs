// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.CommandLine;
using PowerToys.Settings.Cli.Helpers;

namespace PowerToys.Settings.Cli.Commands;

internal sealed class SetCommand : Command
{
    public SetCommand()
        : base("set", "Set a setting property value (e.g. 'FancyZones.FancyzonesShiftDrag true' or 'Enabled.AlwaysOnTop false')")
    {
        var settingArg = new Argument<string>("setting", "Property name in format 'Module.SettingName' or 'Enabled.ModuleName'");
        var valueArg = new Argument<string>("value", "New value for the setting");

        AddArgument(settingArg);
        AddArgument(valueArg);

        this.SetHandler(
            (string setting, string value) =>
            {
                var exitCode = Execute(setting, value);
                Environment.ExitCode = exitCode;
            },
            settingArg,
            valueArg);
    }

    private static int Execute(string setting, string value)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(setting) || string.IsNullOrWhiteSpace(value))
            {
                Console.Error.WriteLine("Both setting and value arguments are required.");
                return 1;
            }

            SettingsCliHelper.SetSettingValue(setting, value);
            Console.WriteLine($"Successfully updated setting '{setting}' to '{value}'.");
            return 0;
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine($"Invalid setting specification: {ex.Message}");
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to set setting: {ex.Message}");
            return 1;
        }
    }
}
