// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace PowerToys.DSC.Models.FancyZones;

/// <summary>
/// A quick layout hotkey that applies a custom layout when its number key is
/// pressed together with the quick layout switch modifiers.
/// </summary>
public sealed class FzLayoutHotkey
{
    /// <summary>
    /// Gets or sets the number key (0 to 9).
    /// </summary>
    [JsonPropertyName("key")]
    [Required]
    [Description("The number key, 0 to 9. Each key can be assigned to one layout only.")]
    public int Key { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the custom layout to apply.
    /// </summary>
    [JsonPropertyName("layoutId")]
    [Required]
    [Description("The identifier (GUID) of the custom layout to apply. Each layout can be assigned to one key only.")]
    public string LayoutId { get; set; } = string.Empty;
}
