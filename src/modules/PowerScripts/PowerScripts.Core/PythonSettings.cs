// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Serialization;
using PowerScripts.Core.Storage;

namespace PowerScripts.Core;

/// <summary>How Python PowerScripts are executed.</summary>
public enum PythonRuntimeMode
{
    /// <summary>Python PowerScripts are not run.</summary>
    Disabled,

    /// <summary>Run using native Windows Python (auto-detected or a configured interpreter path).</summary>
    Windows,

    /// <summary>Run inside a WSL distribution.</summary>
    Wsl,
}

/// <summary>Windows-mode Python settings.</summary>
public sealed class PythonWindowsSettings
{
    /// <summary>Optional explicit interpreter path; empty means auto-detect (python.exe / python3.exe / py.exe).</summary>
    [JsonPropertyName("interpreterPath")]
    public string InterpreterPath { get; set; } = string.Empty;
}

/// <summary>WSL-mode Python settings.</summary>
public sealed class PythonWslSettings
{
    /// <summary>The WSL distribution to run scripts in; empty means the default distribution.</summary>
    [JsonPropertyName("distribution")]
    public string Distribution { get; set; } = string.Empty;
}

/// <summary>
/// The Python runtime configuration for PowerScripts, persisted in the module's <c>config.json</c>
/// (alongside <c>scriptsRoot</c>). Mirrors the Advanced Paste Python settings (mode + Windows/WSL
/// sub-settings) so users see a familiar shape and the two features can converge.
/// </summary>
public sealed class PythonSettings
{
    [JsonPropertyName("mode")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public PythonRuntimeMode Mode { get; set; } = PythonRuntimeMode.Disabled;

    [JsonPropertyName("windows")]
    public PythonWindowsSettings Windows { get; set; } = new();

    [JsonPropertyName("wsl")]
    public PythonWslSettings Wsl { get; set; } = new();

    /// <summary>Hard timeout for a single Python run, in seconds.</summary>
    [JsonPropertyName("timeoutSeconds")]
    public int TimeoutSeconds { get; set; } = 30;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>
    /// Loads the Python settings from the module <c>config.json</c>'s <c>python</c> section, falling
    /// back to defaults (disabled) when absent or unreadable.
    /// </summary>
    public static PythonSettings Load()
    {
        try
        {
            var text = SettingsStore.Current.ReadBlob(PowerScriptsPaths.ConfigFileName);
            if (string.IsNullOrEmpty(text))
            {
                return new PythonSettings();
            }

            using var document = JsonDocument.Parse(text);
            if (document.RootElement.TryGetProperty("python", out var python) &&
                python.ValueKind == JsonValueKind.Object)
            {
                return python.Deserialize<PythonSettings>(SerializerOptions) ?? new PythonSettings();
            }
        }
        catch (Exception)
        {
            // A corrupt or unreadable config simply falls back to defaults (Python disabled).
        }

        return new PythonSettings();
    }

    /// <summary>
    /// Persists these settings into the module config's <c>python</c> section, preserving any other
    /// keys (e.g. <c>scriptsRoot</c>).
    /// </summary>
    public void Save()
    {
        var root = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        try
        {
            var text = SettingsStore.Current.ReadBlob(PowerScriptsPaths.ConfigFileName);
            if (!string.IsNullOrEmpty(text))
            {
                using var existing = JsonDocument.Parse(text);
                foreach (var property in existing.RootElement.EnumerateObject())
                {
                    root[property.Name] = property.Value.Clone();
                }
            }
        }
        catch (Exception)
        {
            // Overwrite a corrupt config rather than fail.
        }

        root["python"] = JsonSerializer.SerializeToElement(this, SerializerOptions);
        SettingsStore.Current.WriteBlob(PowerScriptsPaths.ConfigFileName, JsonSerializer.Serialize(root, SerializerOptions));
    }
}
