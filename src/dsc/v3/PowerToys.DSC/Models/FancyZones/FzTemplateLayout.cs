// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace PowerToys.DSC.Models.FancyZones;

/// <summary>
/// The settings of a built-in layout template.
/// </summary>
public sealed class FzTemplateLayout
{
    /// <summary>
    /// Gets or sets the template type.
    /// </summary>
    [JsonPropertyName("type")]
    [Required]
    [Description("The template type: blank, focus, rows, columns, grid, or priority-grid.")]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the number of zones.
    /// </summary>
    [JsonPropertyName("zoneCount")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [Description("The number of zones. Default 3.")]
    public int? ZoneCount { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether space is left between zones.
    /// </summary>
    [JsonPropertyName("showSpacing")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [Description("Whether space is left between the zones. Default true; not applicable to the blank and focus templates.")]
    public bool? ShowSpacing { get; set; }

    /// <summary>
    /// Gets or sets the space between zones.
    /// </summary>
    [JsonPropertyName("spacing")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [Description("The space between the zones, in pixels. Default 16; not applicable to the blank and focus templates.")]
    public int? Spacing { get; set; }

    /// <summary>
    /// Gets or sets the distance from a zone edge at which highlighting starts.
    /// </summary>
    [JsonPropertyName("sensitivityRadius")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [Description("The distance, in pixels, from a zone edge at which the zone is highlighted while dragging. Default 20.")]
    public int? SensitivityRadius { get; set; }
}
