// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
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

/// <summary>
/// Describes a program started by a shortcut remapping.
/// </summary>
public sealed class KbmRunProgramAction
{
    /// <summary>
    /// Gets or sets the path of the program to start. Environment variables are expanded.
    /// </summary>
    [JsonPropertyName("filePath")]
    [Required]
    [Description("The path of the program to start. Environment variables are expanded.")]
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the command-line arguments passed to the program.
    /// </summary>
    [JsonPropertyName("args")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [Description("The command-line arguments passed to the program.")]
    public string? Args { get; set; }

    /// <summary>
    /// Gets or sets the working directory the program is started in.
    /// </summary>
    [JsonPropertyName("startInDir")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [Description("The working directory the program is started in.")]
    public string? StartInDir { get; set; }

    /// <summary>
    /// Gets or sets the elevation level the program is started with:
    /// "normal", "elevated", or "differentUser".
    /// </summary>
    [JsonPropertyName("elevation")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [Description("The elevation level the program is started with: \"normal\" (default), \"elevated\", or \"differentUser\".")]
    public string? Elevation { get; set; }

    /// <summary>
    /// Gets or sets what happens when the program is already running:
    /// "showWindow", "startAnother", "doNothing", "close", "endTask", or
    /// "closeAndEndTask".
    /// </summary>
    [JsonPropertyName("ifRunning")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [Description("What happens when the program is already running: \"showWindow\" (default), \"startAnother\", \"doNothing\", \"close\", \"endTask\", or \"closeAndEndTask\".")]
    public string? IfRunning { get; set; }

    /// <summary>
    /// Gets or sets the window style the program is started with: "normal",
    /// "hidden", "minimized", or "maximized".
    /// </summary>
    [JsonPropertyName("windowStyle")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [Description("The window style the program is started with: \"normal\" (default), \"hidden\", \"minimized\", or \"maximized\".")]
    public string? WindowStyle { get; set; }
}
