// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace PowerToys.DSC.Models.KeyboardManager;

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
