// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Text.Json.Serialization;

namespace PowerToys.DSC.Models.FancyZones;

/// <summary>
/// The default layouts applied to monitors that have no layout assigned yet,
/// by monitor orientation.
/// </summary>
public sealed class FzDefaultLayouts
{
    public const string HorizontalJsonPropertyName = "horizontal";
    public const string VerticalJsonPropertyName = "vertical";

    /// <summary>
    /// Gets or sets the default layout for horizontal (landscape) monitors.
    /// </summary>
    [JsonPropertyName(HorizontalJsonPropertyName)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [Description("The default layout for horizontal (landscape) monitors.")]
    public FzDefaultLayout? Horizontal { get; set; }

    /// <summary>
    /// Gets or sets the default layout for vertical (portrait) monitors.
    /// </summary>
    [JsonPropertyName(VerticalJsonPropertyName)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [Description("The default layout for vertical (portrait) monitors.")]
    public FzDefaultLayout? Vertical { get; set; }
}
