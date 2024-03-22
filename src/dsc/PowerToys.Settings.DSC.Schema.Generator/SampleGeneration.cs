// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Linq;
using static PowerToys.Settings.DSC.Schema.Introspection;

namespace PowerToys.Settings.DSC.Schema;

internal sealed class SampleGeneration
{
    private const int FixedSeed = 12345;
    private static readonly Random _random = new Random(FixedSeed);

    private static string EmitPropertySetter(string name, ModulePropertyStructure info)
    {
        var randomPropertyValue = "\"<string>\"";
        if (Common.InferIsBool(info.Type))
        {
            randomPropertyValue = _random.Next(2) == 1 ? "true" : "false";
        }
        else if (Common.InferIsInt(info.Type))
        {
            randomPropertyValue = _random.Next(256).ToString(CultureInfo.InvariantCulture);
        }
        else if (info.Type.IsEnum)
        {
            var enumValues = Enum.GetValues(info.Type);
            randomPropertyValue = enumValues.GetValue(_random.Next(enumValues.Length)).ToString();
        }

        return $"        {name}: {randomPropertyValue}";
    }

    private static string EmitModulePropertiesSection(SettingsStructure module)
    {
        bool generalSettings = module.Name == "GeneralSettings";

        var propertiesCollection = module.Properties
            .Where(p => !p.Value.IsIgnored)
            .Select(property => EmitPropertySetter(property.Key, property.Value))
            .ToList();

        string properties = propertiesCollection.Count != 0
            ? propertiesCollection.Aggregate((acc, line) => string.Join(Environment.NewLine, acc, line))
            : string.Empty;

        var propertyDefinitionsBlock = string.Empty;
        var applyChangesBlock = string.Empty;
        return $$"""
      {{module.Name}}:
{{properties}}


""";
    }

    public static string EmitSampleFileContents(SettingsStructure[] moduleSettings, SettingsStructure generalSettings)
    {
        var moduleTables = $$"""
properties:
resources:
    - resource: PowerToysConfigure
    directives:
        description: Configure PowerToys
    settings:

""";

        foreach (var module in moduleSettings.Append(generalSettings))
        {
            moduleTables += EmitModulePropertiesSection(module);
        }

        moduleTables += "  configurationVersion: 0.2.0";
        return moduleTables;
    }
}
