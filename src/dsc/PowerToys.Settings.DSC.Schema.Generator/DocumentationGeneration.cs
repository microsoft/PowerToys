// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using static PowerToys.Settings.DSC.Schema.Introspection;

namespace PowerToys.Settings.DSC.Schema;

internal sealed class DocumentationGeneration
{
    private static readonly string IsAvailableSymbol = "✅";
    private static readonly string IsUnavailableSymbol = "❌";
    private static readonly string MissingValueIndicator = "—";

    private static readonly string PropertySuffix = "Property";

    private static string SimplifyPropertyType(string typeName)
    {
        if (typeName.EndsWith(PropertySuffix, StringComparison.InvariantCulture))
        {
            typeName = typeName.Remove(typeName.LastIndexOf(PropertySuffix, StringComparison.InvariantCulture), PropertySuffix.Length);
        }

        return typeName;
    }

    private static string EmitPropertyTableLine(string name, ModulePropertyStructure info)
    {
        bool isAvailable = !info.IsIgnored;
        var availabilitySymbol = isAvailable ? IsAvailableSymbol : IsUnavailableSymbol;
        var documentation = MissingValueIndicator;
        if (info.Type.IsEnum)
        {
            documentation = "Possible values: ";
            foreach (var enumValue in Enum.GetValues(info.Type))
            {
                documentation += enumValue.ToString() + ' ';
            }
        }

        var propertyType = isAvailable ? SimplifyPropertyType(info.Type.Name) : MissingValueIndicator;
        return $"| {name} | {propertyType} | {documentation} | {availabilitySymbol} |";
    }

    private static string EmitModulePropertiesTable(SettingsStructure module)
    {
        bool generalSettings = module.Name == "GeneralSettings";

        var properties = module.Properties
                .Where(p => !p.Value.IsIgnoredByJsonSerializer)
                .Select(property => EmitPropertyTableLine(property.Key, property.Value)).Aggregate((acc, line) => string.Join(Environment.NewLine, [acc, line]));

        var propertyDefinitionsBlock = string.Empty;
        var applyChangesBlock = string.Empty;
        return $$"""
### {{module.Name}}

| Name | Type | Description | Available |
| :--- | :--- | :--- | :--- |
{{properties}}


""";
    }

    public static string EmitDocumentationFileContents(SettingsStructure[] moduleSettings, SettingsStructure generalSettings)
    {
        var moduleTables = string.Empty;

        foreach (var module in moduleSettings.Append(generalSettings))
        {
            moduleTables += EmitModulePropertiesTable(module);
        }

        return moduleTables;
    }
}
