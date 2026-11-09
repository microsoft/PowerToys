// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace PowerToys.DSC.Models.KeyboardManager;

/// <summary>
/// Friendly, hand-authorable representation of the Keyboard Manager remapping
/// profile used by the DSC profile resource. Keys are expressed with friendly
/// names (e.g. "CapsLock", "Ctrl+Shift+A", or "Win+O, K" for a chord) instead
/// of the virtual-key code strings stored in the profile file.
/// </summary>
public sealed class KbmProfileModel
{
    /// <summary>
    /// Gets or sets the single-key remappings.
    /// </summary>
    [JsonPropertyName("keys")]
    [Description("Single-key remappings. Each entry remaps one key to a key, a shortcut, or text.")]
    public List<KbmKeyRemapEntry> Keys { get; set; } = [];

    /// <summary>
    /// Gets or sets the shortcut remappings.
    /// </summary>
    [JsonPropertyName("shortcuts")]
    [Description("Shortcut remappings. Each entry remaps a shortcut to a key, a shortcut, text, a program, or a URI; optionally scoped to a target application.")]
    public List<KbmShortcutRemapEntry> Shortcuts { get; set; } = [];
}
