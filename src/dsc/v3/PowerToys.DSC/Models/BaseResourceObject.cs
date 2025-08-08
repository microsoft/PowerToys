// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace PowerToys.DSC.Models;

internal class BaseResourceObject
{
    private readonly JsonSerializerOptions _serializerOptions;

    public BaseResourceObject()
    {
        _serializerOptions = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        };
    }

    [JsonPropertyName("_inDesiredState")]
    public bool? InDesiredState { get; set; }

    public JsonNode ToJson()
    {
        return JsonSerializer.SerializeToNode(this, GetType(), _serializerOptions) ?? new JsonObject();
    }
}
