// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace PowerToys.DSC.Models.FancyZones;

/// <summary>
/// A custom layout. Exactly one of <see cref="Canvas"/> or <see cref="Grid"/>
/// must be set; it determines the layout type.
/// </summary>
public sealed class FzCustomLayout
{
    /// <summary>
    /// Gets or sets the layout identifier.
    /// </summary>
    [JsonPropertyName("uuid")]
    [Required]
    [Description("The layout identifier: a GUID, with or without braces, e.g. \"{5C4F1A20-9B3E-4C7D-8E2F-1A2B3C4D5E6F}\".")]
    public string Uuid { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the layout name.
    /// </summary>
    [JsonPropertyName("name")]
    [Required]
    [Description("The layout name shown in the FancyZones editor.")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the canvas layout definition.
    /// </summary>
    [JsonPropertyName("canvas")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [Description("The canvas layout definition (free-form zones). Exactly one of 'canvas' or 'grid' must be set.")]
    public FzCanvasInfo? Canvas { get; set; }

    /// <summary>
    /// Gets or sets the grid layout definition.
    /// </summary>
    [JsonPropertyName("grid")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [Description("The grid layout definition (rows and columns). Exactly one of 'canvas' or 'grid' must be set.")]
    public FzGridInfo? Grid { get; set; }
}
