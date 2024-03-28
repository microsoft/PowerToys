// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Xml;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Settings.UI.Library.Attributes;

namespace Microsoft.PowerToys.Settings.UI.Library;

/// <summary>
/// This user flow allows DSC resources to use PowerToys.Settings executable to get settings values by querying them from command line using the following syntax:
/// PowerToys.Settings.exe get <path to a json file containing a list of modules and their corresponding properties>
///
/// Example: PowerToys.Settings.exe get %TEMP%\properties.json
/// `properties.json` file contents:
/// {
///   "AlwaysOnTop": ["FrameEnabled", "FrameAccentColor"],
///   "FancyZones": ["FancyzonesShiftDrag", "FancyzonesShowOnAllMonitors"]
/// }
///
/// Upon PowerToys.Settings.exe completion, it'll update `properties.json` file to contain something like this:
/// {
///   "AlwaysOnTop": {
///     "FrameEnabled": true,
///     "FrameAccentColor": "#0099cc"
///   },
///   "FancyZones": {
///     "FancyzonesShiftDrag": true,
///     "FancyzonesShowOnAllMonitors": false
///   }
/// }
/// </summary>
public sealed class GetSettingCommandLineCommand
{
    private static JsonSerializerOptions _serializerOptions = new JsonSerializerOptions
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string Execute(Dictionary<string, List<string>> settingNamesForModules)
    {
        var modulesSettings = new Dictionary<string, Dictionary<string, object>>();

        var settingsAssembly = CommandLineUtils.GetSettingsAssembly();
        var settingsUtils = new SettingsUtils();

        var enabledModules = SettingsRepository<GeneralSettings>.GetInstance(settingsUtils).SettingsConfig.Enabled;

        foreach (var (moduleName, settings) in settingNamesForModules)
        {
            var moduleSettings = new Dictionary<string, object>();
            if (moduleName != nameof(GeneralSettings))
            {
                moduleSettings.Add("Enabled", typeof(EnabledModules).GetProperty(moduleName).GetValue(enabledModules));
            }

            var settingsConfig = CommandLineUtils.GetSettingsConfigFor(moduleName, settingsUtils, settingsAssembly);
            foreach (var settingName in settings)
            {
                var value = CommandLineUtils.GetPropertyValue(settingName, settingsConfig);
                if (value != null)
                {
                    var cmdReprValue = ICmdLineRepresentable.ToCmdRepr(value.GetType(), value);
                    moduleSettings.Add(settingName, cmdReprValue);
                }
            }

            modulesSettings.Add(moduleName, moduleSettings);
        }

        return JsonSerializer.Serialize(modulesSettings, _serializerOptions);
    }
}
