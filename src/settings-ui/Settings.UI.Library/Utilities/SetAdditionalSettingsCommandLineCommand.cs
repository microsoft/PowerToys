// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Settings.UI.Library.Attributes;

namespace Microsoft.PowerToys.Settings.UI.Library;

/// <summary>
/// This user flow allows DSC resources to use PowerToys.Settings executable to set custom settings values by suppling them from command line using the following syntax:
/// PowerToys.Settings.exe setAdditional <module struct name> <path to a json file containing the properties>
/// </summary>
public sealed class SetAdditionalSettingsCommandLineCommand
{
    private static readonly string KeyPropertyName = "Name";

    private static JsonSerializerOptions _serializerOptions = new JsonSerializerOptions
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private struct AdditionalPropertyInfo
    {
        public string RootPropertyName;
        public JsonValueKind RootObjectType;
    }

    private static readonly Dictionary<string, AdditionalPropertyInfo> SupportedAdditionalPropertiesInfoForModules = new Dictionary<string, AdditionalPropertyInfo> { { "PowerLauncher", new AdditionalPropertyInfo { RootPropertyName = "Plugins", RootObjectType = JsonValueKind.Array } } };

    private static void ExecuteRootArray(JsonElement.ArrayEnumerator properties, IEnumerable<object> currentPropertyValuesArray)
    {
        // In case it's an array of object -> combine the existing values with the provided
        var currentPropertyValueType = currentPropertyValuesArray.FirstOrDefault()?.GetType();

        object matchedElement = null;
        foreach (var arrayElement in properties)
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

    public static void Execute(string moduleName, JsonDocument settings, ISettingsUtils settingsUtils)
    {
        Assembly settingsLibraryAssembly = CommandLineUtils.GetSettingsAssembly();

        var settingsConfig = CommandLineUtils.GetSettingsConfigFor(moduleName, settingsUtils, settingsLibraryAssembly);
        var settingsConfigType = settingsConfig.GetType();

        if (!SupportedAdditionalPropertiesInfoForModules.TryGetValue(moduleName, out var additionalPropertiesInfo))
        {
            return;
        }

        var propertyValueInfo = settingsConfigType.GetProperty(additionalPropertiesInfo.RootPropertyName);
        var currentPropertyValue = propertyValueInfo.GetValue(settingsConfig);

        // For now, only a certain data shapes are supported
        switch (additionalPropertiesInfo.RootObjectType)
        {
            case JsonValueKind.Array:
                ExecuteRootArray(settings.RootElement.EnumerateArray(), currentPropertyValue as IEnumerable<object>);
                break;
            default:
                throw new NotImplementedException();
        }

        settingsUtils.SaveSettings(settingsConfig.ToJsonString(), settingsConfig.GetModuleName());
    }
}
