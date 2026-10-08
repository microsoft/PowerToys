// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace PowerToys.DSC.Models.FancyZones;

/// <summary>
/// The definition of a canvas layout: free-form zones positioned on a
/// reference work area.
/// </summary>
public sealed class FzCanvasInfo
{
    /// <summary>
    /// Gets or sets the width of the reference work area the zones are defined for.
    /// </summary>
    [JsonPropertyName("refWidth")]
    [Required]
    [Description("The width, in pixels, of the work area the zones are defined for. Zones are scaled to the actual work area.")]
    public int RefWidth { get; set; }

    /// <summary>
    /// Gets or sets the height of the reference work area the zones are defined for.
    /// </summary>
    [JsonPropertyName("refHeight")]
    [Required]
    [Description("The height, in pixels, of the work area the zones are defined for. Zones are scaled to the actual work area.")]
    public int RefHeight { get; set; }

    /// <summary>
    /// Gets or sets the zones.
    /// </summary>
    [JsonPropertyName("zones")]
    [Required]
    [Description("The zones of the layout (1 to 128).")]
    public List<FzCanvasZone> Zones { get; set; } = [];

    /// <summary>
    /// Gets or sets the distance from a zone edge at which highlighting starts.
    /// </summary>
    [JsonPropertyName("sensitivityRadius")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [Description("The distance, in pixels, from a zone edge at which the zone is highlighted while dragging. Default 20.")]
    public int? SensitivityRadius { get; set; }
}
