// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;
using Settings.UI.Library.Attributes;

namespace Microsoft.PowerToys.Settings.UI.Library;

/// <summary>
/// This user flow allows DSC resources to use PowerToys.Settings executable to set settings values by suppling them from command line using the following syntax:
/// PowerToys.Settings.exe set <module struct name>.<field name> <field_value>
///
/// Example: PowerToys.Settings.exe set MeasureTool.MeasureCrossColor "#00FF00"
/// </summary>
public sealed class SetSettingCommandLineCommand
{
    private static readonly char[] SettingNameSeparator = { '.' };

    private static (string ModuleName, string PropertyName) ParseSettingName(string settingName)
    {
        var parts = settingName.Split(SettingNameSeparator, 2, StringSplitOptions.RemoveEmptyEntries);
        return (parts[0], parts[1]);
    }

    public static void Execute(string settingName, string settingValue, ISettingsUtils settingsUtils)
    {
        Assembly settingsLibraryAssembly = CommandLineUtils.GetSettingsAssembly();

        var (moduleName, propertyName) = ParseSettingName(settingName);

        var settingsConfig = CommandLineUtils.GetSettingsConfigFor(moduleName, settingsUtils, settingsLibraryAssembly);

        var propertyInfo = CommandLineUtils.GetSettingPropertyInfo(propertyName, settingsConfig);
        if (propertyInfo == null)
        {
            throw new ArgumentException($"Property '{propertyName}' wasn't found");
        }

        if (propertyInfo.PropertyType.GetCustomAttribute<CmdConfigureIgnoreAttribute>() != null)
        {
            throw new ArgumentException($"Property '{propertyName}' is explicitly ignored");
        }

        // Execute settingsConfig.Properties.<propertyName> = settingValue
        var propertyValue = ICmdLineRepresentable.ParseFor(propertyInfo.PropertyType, settingValue);
        var (settingInfo, properties) = CommandLineUtils.LocateSetting(propertyName, settingsConfig);
        settingInfo.SetValue(properties, propertyValue);

        settingsUtils.SaveSettings(settingsConfig.ToJsonString(), settingsConfig.GetModuleName());
    }
}
