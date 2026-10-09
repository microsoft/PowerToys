// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Newtonsoft.Json;
using NJsonSchema.Generation;
using PowerToys.DSC.Models.ResourceObjects;

namespace PowerToys.DSC.Models.FunctionData;

/// <summary>
/// Base class for function data objects.
/// </summary>
public class BaseFunctionData
{
    /// <summary>
    /// Generates a JSON schema for the specified resource object type.
    /// </summary>
    /// <typeparam name="T">The type of the resource object.</typeparam>
    /// <returns>A JSON schema string.</returns>
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
