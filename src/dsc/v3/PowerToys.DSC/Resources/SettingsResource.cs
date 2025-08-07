// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text.Json.Serialization;
using Microsoft.PowerToys.Settings.UI.Library;
using NJsonSchema.Generation;
using PowerToys.DSC.Models;

namespace PowerToys.DSC.Resources;

internal sealed class SettingsResource : BaseResource
{
    public const string ResourceName = "settings";

    private static SettingsUtils _settingsUtils = new();

    public SettingsResource(string? moduleName)
        : base(moduleName)
    {
    }

    public override bool Export()
    {
        return Get();
    }

    public override bool Get()
    {
        var settings = _settingsUtils.GetSettings<AwakeSettings>(AwakeSettings.ModuleName);
        WriteJsonOutputLine(settings);
        return true;
    }

    public override void Manifest()
    {
        throw new System.NotImplementedException();
    }

    public override void Schema()
    {
        var settings = new SystemTextJsonSchemaGeneratorSettings()
        {
            FlattenInheritanceHierarchy = true,
            UseXmlDocumentation = true,
            GenerateEnumMappingDescription = false,
            SerializerOptions =
            {
                IgnoreReadOnlyFields = true,
                Converters =
                {
                    new JsonStringEnumConverter(),
                },
            },
        };
        var generator = new JsonSchemaGenerator(settings);
        var schema = generator.Generate(typeof(SettingsResourceObject));
        Console.WriteLine(schema.ToJson());
    }

    public override bool Set()
    {
        throw new System.NotImplementedException();
    }

    public override bool Test()
    {
        throw new System.NotImplementedException();
    }
}
