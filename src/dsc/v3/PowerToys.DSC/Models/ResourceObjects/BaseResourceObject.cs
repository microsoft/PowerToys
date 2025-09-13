// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace PowerToys.DSC.Models.ResourceObjects;

/// <summary>
/// Base class for all resource objects.
/// </summary>
public class BaseResourceObject
{
    private readonly JsonSerializerOptions _options;

    public BaseResourceObject()
    {
        _options = new()
        {
            WriteIndented = false,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        };
    }

    /// <summary>
    /// Gets or sets whether an instance is in the desired state.
    /// </summary>
    [JsonPropertyName("_inDesiredState")]
    [Description("Indicates whether an instance is in the desired state")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? InDesiredState { get; set; }

    /// <summary>
    /// Generates a JSON representation of the resource object.
    /// </summary>
    /// <returns></returns>
    public JsonNode ToJson()
    {
        return JsonSerializer.SerializeToNode(this, GetType(), _options) ?? new JsonObject();
    }
}
