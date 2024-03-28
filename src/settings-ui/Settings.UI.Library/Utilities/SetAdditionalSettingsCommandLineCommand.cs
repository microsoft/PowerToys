// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Settings.UI.Library.Attributes;

namespace Microsoft.PowerToys.Settings.UI.Library;

/// <summary>
/// This user flow allows DSC resources to use PowerToys.Settings executable to set custom settings values by suppling them from command line using the following syntax:
/// PowerToys.Settings.exe setAdditional <module struct name> <path to a json file containing the properties>
///
/// Example: PowerToys.Settings.exe set MeasureTool.MeasureCrossColor "#00FF00"
/// </summary>
public sealed class SetAdditionalSettingsCommandLineCommand
{
    private static readonly string KeyPropertyName = "Name";

    private static JsonSerializerOptions _serializerOptions = new JsonSerializerOptions
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static void Execute(string moduleName, JsonDocument settings, ISettingsUtils settingsUtils)
    {
        Assembly settingsLibraryAssembly = CommandLineUtils.GetSettingsAssembly();

        var settingsConfig = CommandLineUtils.GetSettingsConfigFor(moduleName, settingsUtils, settingsLibraryAssembly);
        var settingsConfigType = settingsConfig.GetType();

        // For now, only a certain data shapes are supported
        foreach (var property in settings.RootElement.EnumerateObject())
        {
            var propertyName = property.Name;
            var propertyValueInfo = settingsConfigType.GetProperty(propertyName);
            var currentPropertyValue = propertyValueInfo.GetValue(settingsConfig);

            // In case it's an array of object -> combine the existing values with the provided
            if (property.Value.ValueKind == JsonValueKind.Array)
            {
                var currentPropertyValuesArray = currentPropertyValue as IEnumerable<object>;
                var currentPropertyValueType = currentPropertyValuesArray.FirstOrDefault()?.GetType();

                object matchedElement = null;
                foreach (var arrayElement in property.Value.EnumerateArray())
                {
                    var newElementPropertyValues = new Dictionary<string, object>();
                    foreach (var elementProperty in arrayElement.EnumerateObject())
                    {
                        var elementPropertyName = elementProperty.Name;
                        var elementPropertyType = currentPropertyValueType.GetProperty(elementPropertyName).PropertyType;
                        var elemePropertyValue = ICmdLineRepresentable.ParseFor(elementPropertyType, elementProperty.Value.ToString());
                        if (elementPropertyName == KeyPropertyName)
                        {
                            foreach (var currentElementValue in currentPropertyValuesArray)
                            {
                                var currentElementType = currentElementValue.GetType();
                                var keyPropertyNameInfo = currentElementType.GetProperty(KeyPropertyName);
                                var keyPropertyValue = keyPropertyNameInfo.GetValue(currentElementValue);
                                if (string.Equals(keyPropertyValue, elemePropertyValue))
                                {
                                    matchedElement = currentElementValue;
                                    break;
                                }
                            }
                        }
                        else
                        {
                            newElementPropertyValues.Add(elementPropertyName, elemePropertyValue);
                        }
                    }

                    if (matchedElement != null)
                    {
                        foreach (var overriddenProperty in newElementPropertyValues)
                        {
                            var propertyInfo = currentPropertyValueType.GetProperty(overriddenProperty.Key);
                            propertyInfo.SetValue(matchedElement, overriddenProperty.Value);
                        }
                    }
                }
            }
        }

        // Execute settingsConfig.Properties.<propertyName> = settingValue
        // var currentPropertyValue = ICmdLineRepresentable.ParseFor(propertyInfo.PropertyType, settingValue);
        // var (settingInfo, properties) = CommandLineUtils.LocateSetting(propertyName, settingsConfig);
        // settingInfo.SetValue(properties, currentPropertyValue);
        settingsUtils.SaveSettings(settingsConfig.ToJsonString(), settingsConfig.GetModuleName());
    }
}
