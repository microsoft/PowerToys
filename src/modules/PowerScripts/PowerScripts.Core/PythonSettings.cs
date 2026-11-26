// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using PowerScripts.Core.Storage;

namespace PowerScripts.Core;

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

    /// <summary>Gets or sets hard timeout for a single Python run, in seconds.</summary>
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
                var configuration = JsonNode.Parse(python.GetRawText())!.AsObject();
                if (string.Equals(configuration["mode"]?.GetValue<string>(), "managedLinux", StringComparison.OrdinalIgnoreCase))
                {
                    configuration["mode"] = "wsl";
                }

                return configuration.Deserialize<PythonSettings>(SerializerOptions) ?? new PythonSettings();
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
