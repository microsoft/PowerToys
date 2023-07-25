// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace MouseJumpUI.Models.Settings.V1;

/// <summary>
/// Represents the "properties" node at the root of the configuration file.
/// </summary>
internal sealed class AppConfig
{
    public AppConfig(
        PropertiesSettings? properties)
    {
        this.Properties = properties;
    }

    [JsonPropertyName("properties")]
    public PropertiesSettings? Properties
    {
        get;
    }
}
