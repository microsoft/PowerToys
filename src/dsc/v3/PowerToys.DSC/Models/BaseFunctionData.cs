// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;
using Newtonsoft.Json;
using NJsonSchema.Generation;

namespace PowerToys.DSC.Models;

internal class BaseFunctionData
{
    protected static string GenerateSchema<T>()
        where T : BaseResourceObject
    {
        var settings = new SystemTextJsonSchemaGeneratorSettings()
        {
            FlattenInheritanceHierarchy = true,
            SerializerOptions =
            {
                IgnoreReadOnlyFields = true,
            },
        };
        var generator = new JsonSchemaGenerator(settings);
        var schema = generator.Generate(typeof(T));
        return schema.ToJson(Formatting.None);
    }
}
