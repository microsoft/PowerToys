// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace PowerToys.DSC.Models.KeyboardManager;

/// <summary>
/// A single-key remapping entry. Exactly one of <see cref="To"/> or
/// <see cref="ToText"/> must be set.
/// </summary>
public class KbmKeyRemapEntry
{
    /// <summary>
    /// Gets or sets the key or shortcut being remapped.
    /// </summary>
    [JsonPropertyName("from")]
    [Required]
    [Description("The key being remapped, e.g. \"CapsLock\".")]
    public string From { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the target key or shortcut, e.g. "Esc", "Ctrl+C", or "Disable".
    /// </summary>
    [JsonPropertyName("to")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [Description("The target key or shortcut, e.g. \"Esc\", \"Ctrl+C\", or \"Disable\".")]
    public string? To { get; set; }

    /// <summary>
    /// Gets or sets the text to type instead of the remapped key or shortcut.
    /// </summary>
    [JsonPropertyName("toText")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [Description("The text to type instead of the remapped key or shortcut.")]
    public string? ToText { get; set; }
}
