// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace PowerToys.DSC.Models.FancyZones;

/// <summary>
/// A zone of a canvas layout.
/// </summary>
public sealed class FzCanvasZone
{
    /// <summary>
    /// Gets or sets the horizontal position of the zone.
    /// </summary>
    [JsonPropertyName("x")]
    [Required]
    [Description("The horizontal position of the zone, in pixels of the reference work area.")]
    public int X { get; set; }

    /// <summary>
    /// Gets or sets the vertical position of the zone.
    /// </summary>
    [JsonPropertyName("y")]
    [Required]
    [Description("The vertical position of the zone, in pixels of the reference work area.")]
    public int Y { get; set; }

    /// <summary>
    /// Gets or sets the width of the zone.
    /// </summary>
    [JsonPropertyName("width")]
    [Required]
    [Description("The width of the zone, in pixels of the reference work area.")]
    public int Width { get; set; }

    /// <summary>
    /// Gets or sets the height of the zone.
    /// </summary>
    [JsonPropertyName("height")]
    [Required]
    [Description("The height of the zone, in pixels of the reference work area.")]
    public int Height { get; set; }
}
