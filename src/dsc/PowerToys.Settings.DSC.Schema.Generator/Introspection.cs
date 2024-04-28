// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization;
using Settings.UI.Library.Attributes;

namespace PowerToys.Settings.DSC.Schema;

public class Introspection
{
    public struct ModulePropertyStructure
    {
        public bool IsIgnoredByJsonSerializer;
        public bool IsIgnoredByCmdConfigureAttribute;

        public bool IsIgnored
        {
            get { return IsIgnoredByJsonSerializer || IsIgnoredByCmdConfigureAttribute; }
        }

        public Type Type;
    }

    public struct SettingsStructure
    {
        public string Name;
        public Dictionary<string, ModulePropertyStructure> Properties;
    }

    private static bool IsModuleNameField(FieldInfo info)
    {
        return info != null && info.IsLiteral && !info.IsInitOnly
            && info.FieldType == typeof(string);
    }

    private static bool IsSettingsClassType(Type type)
    {
        return type.IsClass && type.FullName.EndsWith("Settings", StringComparison.InvariantCulture);
    }

    private static Dictionary<string, ModulePropertyStructure> ParseProperties(Type propertiesType)
    {
        return propertiesType.GetProperties().Select(property =>
        {
            var jsonIgnoreAttr = property.GetCustomAttribute<JsonIgnoreAttribute>();
            var cmdIgnoreAttr = property.GetCustomAttribute<CmdConfigureIgnoreAttribute>();

            return (property.Name, new ModulePropertyStructure
            {
                Type = property.PropertyType,
                IsIgnoredByJsonSerializer = jsonIgnoreAttr != null,
                IsIgnoredByCmdConfigureAttribute = cmdIgnoreAttr != null,
            });
        }).ToDictionary();
    }

    public static SettingsStructure ParseGeneralSettings(Assembly assembly)
    {
        return assembly
            .GetTypes()
            .Where(IsSettingsClassType)
            .Where(type => type.Name == "GeneralSettings")
            .Select(type => new SettingsStructure
            {
                Name = type.Name,
                Properties = ParseProperties(type),
            }).FirstOrDefault();
    }

    public static SettingsStructure[] ParseModuleSettings(Assembly assembly)
    {
        return assembly
            .GetTypes()
            .Where(IsSettingsClassType)
            .Select(type => new
            {
                Properties = type.GetProperty("Properties"),
                ModuleNameInfo = type.GetField("ModuleName", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy),
                TypeName = type.Name,
            })
            .Where(x => x.Properties?.PropertyType.IsClass == true && IsModuleNameField(x.ModuleNameInfo))
            .Select(x => new SettingsStructure
            {
                Name = x.TypeName.Replace("Settings", string.Empty),
                Properties = ParseProperties(x.Properties.PropertyType),
            })
            .ToArray();
    }
}
