// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
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
        // A path to the property starting from the root module Settings object in the following format: "RootPropertyA.NestedPropertyB[...]"
        public string PropertyPath;

        // Property Type hint so we know how to handle it
        public JsonValueKind PropertyType;
    }

    private static readonly Dictionary<string, AdditionalPropertyInfo> SupportedAdditionalPropertiesInfoForModules = new Dictionary<string, AdditionalPropertyInfo> { { "PowerLauncher", new AdditionalPropertyInfo { PropertyPath = "Plugins", PropertyType = JsonValueKind.Array } }, { "ImageResizer", new AdditionalPropertyInfo { PropertyPath = "Properties.ImageresizerSizes.Value", PropertyType = JsonValueKind.Array } } };

    private static IEnumerable<object> ExecuteRootArray(IEnumerable<JsonElement> properties, IEnumerable<object> currentPropertyValuesArray)
    {
        // In case it's an array of objects -> combine the existing values with the provided
        var result = currentPropertyValuesArray;

        var currentPropertyValueType = GetUnderlyingTypeOfCollection(currentPropertyValuesArray);
        object matchedElement = null;

        object newKeyPropertyValue = null;

        foreach (var arrayElement in properties)
        {
            var newElementPropertyValues = new Dictionary<string, object>();
            foreach (var elementProperty in arrayElement.EnumerateObject())
            {
                var elementPropertyName = elementProperty.Name;
                var elementPropertyType = currentPropertyValueType.GetProperty(elementPropertyName).PropertyType;
                var elementNewPropertyValue = ICmdLineRepresentable.ParseFor(elementPropertyType, elementProperty.Value.ToString());
                if (elementPropertyName == KeyPropertyName)
                {
                    newKeyPropertyValue = elementNewPropertyValue;
                    foreach (var currentElementValue in currentPropertyValuesArray)
                    {
                        var currentElementType = currentElementValue.GetType();
                        var keyPropertyNameInfo = currentElementType.GetProperty(KeyPropertyName);
                        var currentKeyPropertyValue = keyPropertyNameInfo.GetValue(currentElementValue);
                        if (string.Equals(currentKeyPropertyValue, elementNewPropertyValue))
                        {
                            matchedElement = currentElementValue;
                            break;
                        }
                    }
                }
                else
                {
                    newElementPropertyValues.Add(elementPropertyName, elementNewPropertyValue);
                }
            }

            // Appending a new element -> create it first using a default ctor with 0 args and append it to the result
            if (matchedElement == null)
            {
                newElementPropertyValues.Add(KeyPropertyName, newKeyPropertyValue);
                matchedElement = Activator.CreateInstance(currentPropertyValueType);
                if (matchedElement != null)
                {
                    result = result.Append(matchedElement);
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

            matchedElement = null;
        }

        return result;
    }

    private static object GetNestedPropertyValue(object obj, string propertyPath)
    {
        if (obj == null || string.IsNullOrWhiteSpace(propertyPath))
        {
            return null;
        }

        var properties = propertyPath.Split('.');
        object currentObject = obj;
        PropertyInfo currentProperty = null;

        foreach (var property in properties)
        {
            if (currentObject == null)
            {
                return null;
            }

            currentProperty = currentObject.GetType().GetProperty(property);
            if (currentProperty == null)
            {
                return null;
            }

            currentObject = currentProperty.GetValue(currentObject);
        }

        return currentObject;
    }

    // To apply changes to a generic collection, we must recreate it and assign it to the property
    private static object CreateCompatibleCollection(Type collectionType, Type elementType, IEnumerable<object> newValues)
    {
        if (typeof(IList<>).MakeGenericType(elementType).IsAssignableFrom(collectionType) ||
            typeof(ObservableCollection<>).MakeGenericType(elementType).IsAssignableFrom(collectionType))
        {
            var concreteType = typeof(List<>).MakeGenericType(elementType);
            if (typeof(ObservableCollection<>).MakeGenericType(elementType).IsAssignableFrom(collectionType))
            {
                concreteType = typeof(ObservableCollection<>).MakeGenericType(elementType);
            }
            else if (collectionType.IsInterface || collectionType.IsAbstract)
            {
                concreteType = typeof(List<>).MakeGenericType(elementType);
            }

            var list = (IList)Activator.CreateInstance(concreteType);
            foreach (var newValue in newValues)
            {
                list.Add(Convert.ChangeType(newValue, elementType, CultureInfo.InvariantCulture));
            }

            return list;
        }
        else if (typeof(IEnumerable<>).MakeGenericType(elementType).IsAssignableFrom(collectionType))
        {
            var listType = typeof(List<>).MakeGenericType(elementType);
            var list = (IList)Activator.CreateInstance(listType);
            foreach (var newValue in newValues)
            {
                list.Add(Convert.ChangeType(newValue, elementType, CultureInfo.InvariantCulture));
            }

            return list;
        }

        return null;
    }

    private static void SetNestedPropertyValue(object obj, string propertyPath, IEnumerable<object> newValues)
    {
        if (obj == null || string.IsNullOrWhiteSpace(propertyPath))
        {
            return;
        }

        var properties = propertyPath.Split('.');
        object currentObject = obj;
        PropertyInfo currentProperty = null;

        for (int i = 0; i < properties.Length - 1; i++)
        {
            if (currentObject == null)
            {
                return;
            }

            currentProperty = currentObject.GetType().GetProperty(properties[i]);
            if (currentProperty == null)
            {
                return;
            }

            currentObject = currentProperty.GetValue(currentObject);
        }

        if (currentObject == null)
        {
            return;
        }

        currentProperty = currentObject.GetType().GetProperty(properties.Last());
        if (currentProperty == null)
        {
            return;
        }

        var propertyType = currentProperty.PropertyType;
        var elementType = propertyType.GetGenericArguments()[0];

        var newCollection = CreateCompatibleCollection(propertyType, elementType, newValues);

        if (newCollection != null)
        {
            currentProperty.SetValue(currentObject, newCollection);
        }
    }

    private static Type GetUnderlyingTypeOfCollection(IEnumerable<object> currentPropertyValuesArray)
    {
        Type collectionType = currentPropertyValuesArray.GetType();

        if (!collectionType.IsGenericType)
        {
            throw new ArgumentException("Invalid json data supplied");
        }

        Type[] genericArguments = collectionType.GetGenericArguments();
        if (genericArguments.Length > 0)
        {
            return genericArguments[0];
        }
        else
        {
            throw new ArgumentException("Invalid json data supplied");
        }
    }

    public static void Execute(string moduleName, JsonDocument settings, SettingsUtils settingsUtils)
    {
        Assembly settingsLibraryAssembly = CommandLineUtils.GetSettingsAssembly();

        var settingsConfig = CommandLineUtils.GetSettingsConfigFor(moduleName, settingsUtils, settingsLibraryAssembly);
        var settingsConfigType = settingsConfig.GetType();

        if (!SupportedAdditionalPropertiesInfoForModules.TryGetValue(moduleName, out var additionalPropertiesInfo))
        {
            return;
        }

        var currentPropertyValue = GetNestedPropertyValue(settingsConfig, additionalPropertiesInfo.PropertyPath);

        // For now, only a certain data shapes are supported
        switch (additionalPropertiesInfo.PropertyType)
        {
            case JsonValueKind.Array:
                if (currentPropertyValue == null)
                {
                    currentPropertyValue = new JsonArray();
                }

                IEnumerable<JsonElement> propertiesToSet = null;

                // Powershell ConvertTo-Json call omits wrapping a single value in an array, so we must do it here
                if (settings.RootElement.ValueKind == JsonValueKind.Object)
                {
                    var wrapperArray = new JsonArray();
                    wrapperArray.Add(settings.RootElement);
                    propertiesToSet = (IEnumerable<JsonElement>)wrapperArray.GetEnumerator();
                }
                else if (settings.RootElement.ValueKind == JsonValueKind.Array)
                {
                    propertiesToSet = settings.RootElement.EnumerateArray().AsEnumerable();
                }
                else
                {
                    throw new ArgumentException("Invalid json data supplied");
                }

                var newPropertyValue = ExecuteRootArray(propertiesToSet, currentPropertyValue as IEnumerable<object>);

                SetNestedPropertyValue(settingsConfig, additionalPropertiesInfo.PropertyPath, newPropertyValue);

                break;
            default:
                throw new NotImplementedException();
        }

        settingsUtils.SaveSettings(settingsConfig.ToJsonString(), settingsConfig.GetModuleName());
    }
}
