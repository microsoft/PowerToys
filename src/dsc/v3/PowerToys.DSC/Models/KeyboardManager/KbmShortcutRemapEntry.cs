// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Text.Json.Serialization;

namespace PowerToys.DSC.Models.KeyboardManager;

/// <summary>
/// A shortcut remapping entry. Exactly one of <see cref="KbmKeyRemapEntry.To"/>,
/// <see cref="KbmKeyRemapEntry.ToText"/>, <see cref="RunProgram"/>, or
/// <see cref="OpenUri"/> must be set.
/// </summary>
public sealed class KbmShortcutRemapEntry : KbmKeyRemapEntry
{
    /// <summary>
    /// Gets or sets the process name of the application the remapping applies
    /// to, e.g. "notepad.exe". When not set the remapping is global.
    /// </summary>
    [JsonPropertyName("targetApp")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [Description("The process name of the application the remapping applies to, e.g. \"notepad.exe\". When not set the remapping is global.")]
    public string? TargetApp { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the shortcut only triggers when
    /// no other keys are pressed.
    /// </summary>
    [JsonPropertyName("exactMatch")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [Description("When true, the shortcut only triggers when no other keys are pressed.")]
    public bool? ExactMatch { get; set; }

    /// <summary>
    /// Gets or sets the program to start when the shortcut is pressed.
    /// </summary>
    [JsonPropertyName("runProgram")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [Description("The program to start when the shortcut is pressed.")]
    public KbmRunProgramAction? RunProgram { get; set; }

    /// <summary>
    /// Gets or sets the URI to open when the shortcut is pressed.
    /// </summary>
    [JsonPropertyName("openUri")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [Description("The URI to open when the shortcut is pressed, e.g. \"https://github.com\" or \"ms-settings:\".")]
    public string? OpenUri { get; set; }
}
