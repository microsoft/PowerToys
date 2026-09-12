// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace PowerToys.DSC.Models.FancyZones;

/// <summary>
/// Friendly, hand-authorable representation of the FancyZones layout data
/// managed by the DSC layouts resource.
/// </summary>
public sealed class FzLayoutsModel
{
    public const string CustomJsonPropertyName = "custom";
    public const string TemplatesJsonPropertyName = "templates";
    public const string HotkeysJsonPropertyName = "hotkeys";
    public const string DefaultsJsonPropertyName = "defaults";

    /// <summary>
    /// Gets or sets the custom layouts (custom-layouts.json).
    /// </summary>
    [JsonPropertyName(CustomJsonPropertyName)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [Description("The custom layouts (custom-layouts.json). Each layout is either a canvas layout or a grid layout.")]
    public List<FzCustomLayout>? Custom { get; set; }

    /// <summary>
    /// Gets or sets the layout templates (layout-templates.json).
    /// </summary>
    [JsonPropertyName(TemplatesJsonPropertyName)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [Description("The settings of the built-in layout templates (layout-templates.json), one entry per template type.")]
    public List<FzTemplateLayout>? Templates { get; set; }

    /// <summary>
    /// Gets or sets the quick layout hotkeys (layout-hotkeys.json).
    /// </summary>
    [JsonPropertyName(HotkeysJsonPropertyName)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [Description("The quick layout hotkeys (layout-hotkeys.json) that assign a custom layout to a number key.")]
    public List<FzLayoutHotkey>? Hotkeys { get; set; }

    /// <summary>
    /// Gets or sets the default layouts (default-layouts.json).
    /// </summary>
    [JsonPropertyName(DefaultsJsonPropertyName)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [Description("The default layouts (default-layouts.json) applied to new horizontal and vertical monitors.")]
    public FzDefaultLayouts? Defaults { get; set; }
}
