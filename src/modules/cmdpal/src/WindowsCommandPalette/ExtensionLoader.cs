// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.AppExtensions;
using Windows.ApplicationModel;
using Windows.Foundation.Collections;

namespace DeveloperCommandPalette;
internal sealed class ExtensionLoader
{

    private const string CreateInstanceProperty = "CreateInstance";
    private const string ClassIdProperty = "@ClassId";
    private static IPropertySet? GetSubPropertySet(IPropertySet propSet, string name)
    {
        return propSet.TryGetValue(name, out var value) ? value as IPropertySet : null;
    }

    private static object[]? GetSubPropertySetArray(IPropertySet propSet, string name)
    {
        return propSet.TryGetValue(name, out var value) ? value as object[] : null;
    }

    private static string? GetProperty(IPropertySet propSet, string name)
    {
        return propSet[name] as string;
    }

    /// <summary>
    /// There are cases where the extension creates multiple COM instances.
    /// </summary>
    /// <param name="activationPropSet">Activation property set object</param>
    /// <returns>List of ClassId strings associated with the activation property</returns>
    private static List<string> GetCreateInstanceList(IPropertySet activationPropSet)
    {
        var propSetList = new List<string>();
        var singlePropertySet = GetSubPropertySet(activationPropSet, CreateInstanceProperty);
        if (singlePropertySet != null)
        {
            var classId = GetProperty(singlePropertySet, ClassIdProperty);

            // If the instance has a classId as a single string, then it's only supporting a single instance.
            if (classId != null)
            {
                propSetList.Add(classId);
            }
        }
        else
        {
            var propertySetArray = GetSubPropertySetArray(activationPropSet, CreateInstanceProperty);
            if (propertySetArray != null)
            {
                foreach (var prop in propertySetArray)
                {
                    if (prop is not IPropertySet propertySet)
                    {
                        continue;
                    }

                    var classId = GetProperty(propertySet, ClassIdProperty);
                    if (classId != null)
                    {
                        propSetList.Add(classId);
                    }
                }
            }
        }

        return propSetList;
    }


    public static async Task<(IPropertySet?, List<string>)> GetExtensionPropertiesAsync(AppExtension extension)
    {
        var classIds = new List<string>();
        var properties = await extension.GetExtensionPropertiesAsync();

        if (properties is null)
        {
            return (null, classIds);
        }

        var CmdPalProvider = GetSubPropertySet(properties, "CmdPalProvider");
        if (CmdPalProvider is null)
        {
            return (null, classIds);
        }

        var activation = GetSubPropertySet(CmdPalProvider, "Activation");
        if (activation is null)
        {
            return (CmdPalProvider, classIds);
        }

        // Handle case where extension creates multiple instances.
        classIds.AddRange(GetCreateInstanceList(activation));

        return (CmdPalProvider, classIds);
    }

    private static async Task<bool> IsValidExtension(Package package)
    {
        var extensions = await AppExtensionCatalog.Open("com.microsoft.windows.commandpalette").FindAllAsync();
        foreach (var extension in extensions)
        {
            if (package.Id?.FullName == extension.Package?.Id?.FullName)
            {
                var (CmdPalProvider, classId) = await GetExtensionPropertiesAsync(extension);
                return CmdPalProvider != null && classId.Count != 0;
            }
        }

        return false;
    }
}
