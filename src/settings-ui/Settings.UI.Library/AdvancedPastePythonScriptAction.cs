// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;

namespace Microsoft.PowerToys.Settings.UI.Library;

public sealed class AdvancedPastePythonScriptAction : Observable, IAdvancedPasteAction, ICloneable
{
    private string _scriptPath = string.Empty;
    private string _name = string.Empty;
    private string _description = string.Empty;
    private bool _isShown = true;
    private bool _isEnabled = true;
    private string _platform = "windows";
    private string _formats = "any";
    private string _requires = string.Empty;
    private bool _requiresAutoDetect = true;
    private HotkeySettings _shortcut = new();

    [JsonPropertyName("scriptPath")]
    public string ScriptPath
    {
        get => _scriptPath;
        set => Set(ref _scriptPath, value ?? string.Empty);
    }

    [JsonPropertyName("name")]
    public string Name
    {
        get => _name;
        set => Set(ref _name, value ?? string.Empty);
    }

    [JsonPropertyName("description")]
    public string Description
    {
        get => _description;
        set => Set(ref _description, value ?? string.Empty);
    }

    [JsonPropertyName("isShown")]
    public bool IsShown
    {
        get => _isShown;
        set => Set(ref _isShown, value);
    }

    [JsonPropertyName("isEnabled")]
    public bool IsEnabled
    {
        get => _isEnabled;
        set => Set(ref _isEnabled, value);
    }

    [JsonPropertyName("platform")]
    public string Platform
    {
        get => _platform;
        set => Set(ref _platform, value ?? "windows");
    }

    [JsonPropertyName("formats")]
    public string Formats
    {
        get => _formats;
        set => Set(ref _formats, value ?? "any");
    }

    /// <summary>
    /// Space-separated requires entries, e.g. "cv2=opencv-python-headless numpy requests".
    /// Only written to header when RequiresAutoDetect is false (manual mode).
    /// </summary>
    [JsonPropertyName("requires")]
    public string Requires
    {
        get => _requires;
        set => Set(ref _requires, value ?? string.Empty);
    }

    /// <summary>
    /// When true, dependencies are auto-detected from import statements.
    /// When false, the manual <see cref="Requires"/> value is used.
    /// </summary>
    [JsonPropertyName("requiresAutoDetect")]
    public bool RequiresAutoDetect
    {
        get => _requiresAutoDetect;
        set => Set(ref _requiresAutoDetect, value);
    }

    /// <summary>
    /// Inverted view of RequiresAutoDetect for UI binding.
    /// Uses a separate field to avoid circular property change notifications.
    /// </summary>
    [JsonIgnore]
    public bool IsRequiresManual
    {
        get => !_requiresAutoDetect;
        set
        {
            var newAuto = !value;
            if (_requiresAutoDetect != newAuto)
            {
                _requiresAutoDetect = newAuto;
                OnPropertyChanged(nameof(RequiresAutoDetect));
                OnPropertyChanged(nameof(IsRequiresManual));
            }
        }
    }

    [JsonPropertyName("shortcut")]
    public HotkeySettings Shortcut
    {
        get => _shortcut;
        set
        {
            if (_shortcut != value)
            {
                _shortcut = value ?? new();
                OnPropertyChanged();
            }
        }
    }

    [JsonIgnore]
    public IEnumerable<IAdvancedPasteAction> SubActions => [];

    // Convenience properties for format checkboxes
    [JsonIgnore]
    public bool SupportsText
    {
        get => FormatContains("text");
        set => ToggleFormat("text", value);
    }

    [JsonIgnore]
    public bool SupportsHtml
    {
        get => FormatContains("html");
        set => ToggleFormat("html", value);
    }

    [JsonIgnore]
    public bool SupportsImage
    {
        get => FormatContains("image");
        set => ToggleFormat("image", value);
    }

    [JsonIgnore]
    public bool SupportsAudio
    {
        get => FormatContains("audio");
        set => ToggleFormat("audio", value);
    }

    [JsonIgnore]
    public bool SupportsVideo
    {
        get => FormatContains("video");
        set => ToggleFormat("video", value);
    }

    [JsonIgnore]
    public bool SupportsFiles
    {
        get => FormatContains("files") || FormatContains("file");
        set => ToggleFormat("files", value);
    }

    private bool FormatContains(string format)
    {
        if (string.Equals(Formats, "any", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return Formats.Split(',', StringSplitOptions.TrimEntries)
                       .Any(f => string.Equals(f, format, StringComparison.OrdinalIgnoreCase));
    }

    private bool _isTogglingFormat;

    private void ToggleFormat(string format, bool include)
    {
        if (_isTogglingFormat)
        {
            return;
        }

        _isTogglingFormat = true;
        try
        {
            var currentFormats = string.Equals(Formats, "any", StringComparison.OrdinalIgnoreCase)
                ? new HashSet<string>(["text", "html", "image", "audio", "video", "files"], StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(Formats.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries), StringComparer.OrdinalIgnoreCase);

            // Normalize file/files
            currentFormats.Remove("file");

            if (include)
            {
                currentFormats.Add(format);
            }
            else
            {
                currentFormats.Remove(format);
            }

            var allFormats = new HashSet<string>(["text", "html", "image", "audio", "video", "files"], StringComparer.OrdinalIgnoreCase);
            Formats = currentFormats.SetEquals(allFormats) ? "any" : string.Join(", ", currentFormats);

            OnPropertyChanged(nameof(SupportsText));
            OnPropertyChanged(nameof(SupportsHtml));
            OnPropertyChanged(nameof(SupportsImage));
            OnPropertyChanged(nameof(SupportsAudio));
            OnPropertyChanged(nameof(SupportsVideo));
            OnPropertyChanged(nameof(SupportsFiles));
        }
        finally
        {
            _isTogglingFormat = false;
        }
    }

    public object Clone()
    {
        return new AdvancedPastePythonScriptAction
        {
            ScriptPath = ScriptPath,
            Name = Name,
            Description = Description,
            IsShown = IsShown,
            IsEnabled = IsEnabled,
            Platform = Platform,
            Formats = Formats,
            Requires = Requires,
            RequiresAutoDetect = RequiresAutoDetect,
            Shortcut = Shortcut,
        };
    }
}
