// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace PowerToys.DSC.Models.FancyZones;

/// <summary>
/// The definition of a grid layout: rows and columns whose cells are
/// assigned to zones.
/// </summary>
public sealed class FzGridInfo
{
    /// <summary>
    /// Gets or sets the number of rows.
    /// </summary>
    [JsonPropertyName("rows")]
    [Required]
    [Description("The number of rows.")]
    public int Rows { get; set; }

    /// <summary>
    /// Gets or sets the number of columns.
    /// </summary>
    [JsonPropertyName("columns")]
    [Required]
    [Description("The number of columns.")]
    public int Columns { get; set; }

    /// <summary>
    /// Gets or sets the height of each row.
    /// </summary>
    [JsonPropertyName("rowsPercentage")]
    [Required]
    [Description("The height of each row in hundredths of a percent; one value per row, summing to 10000.")]
    public List<int> RowsPercentage { get; set; } = [];

    /// <summary>
    /// Gets or sets the width of each column.
    /// </summary>
    [JsonPropertyName("columnsPercentage")]
    [Required]
    [Description("The width of each column in hundredths of a percent; one value per column, summing to 10000.")]
    public List<int> ColumnsPercentage { get; set; } = [];

    /// <summary>
    /// Gets or sets the zone index of each cell.
    /// </summary>
    [JsonPropertyName("cellChildMap")]
    [Required]
    [Description("The zone index of each cell, as one array per row with one value per column. Cells with the same index form one zone.")]
    public List<List<int>> CellChildMap { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether space is left between zones.
    /// </summary>
    [JsonPropertyName("showSpacing")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [Description("Whether space is left between the zones. Default true.")]
    public bool? ShowSpacing { get; set; }

    /// <summary>
    /// Gets or sets the space between zones.
    /// </summary>
    [JsonPropertyName("spacing")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [Description("The space between the zones, in pixels. Default 16.")]
    public int? Spacing { get; set; }

    /// <summary>
    /// Gets or sets the distance from a zone edge at which highlighting starts.
    /// </summary>
    [JsonPropertyName("sensitivityRadius")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [Description("The distance, in pixels, from a zone edge at which the zone is highlighted while dragging. Default 20.")]
    public int? SensitivityRadius { get; set; }
}
