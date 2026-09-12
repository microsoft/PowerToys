// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using PowerToys.DSC.Models.FancyZones;

namespace PowerToys.DSC.Models.ResourceObjects;

/// <summary>
/// Represents the resource object for the FancyZones layouts.
/// </summary>
public sealed class LayoutsResourceObject : BaseResourceObject
{
    public const string LayoutsJsonPropertyName = "layouts";

    /// <summary>
    /// Gets or sets the FancyZones layouts.
    /// </summary>
    [JsonPropertyName(LayoutsJsonPropertyName)]
    [Required]
    [Description("The FancyZones layouts: custom layouts, layout templates, layout hotkeys and default layouts. Each section is optional; a section that is present replaces the corresponding layout file, a section that is omitted is left unchanged.")]
    public FzLayoutsModel Layouts { get; set; } = new();
}
