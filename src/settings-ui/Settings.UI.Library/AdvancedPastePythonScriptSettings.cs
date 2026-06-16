// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Library;

public sealed class AdvancedPastePythonScriptSettings
{
    /// <summary>
    /// Execution mode: "disabled", "windows", or "wsl".
    /// </summary>
    [JsonPropertyName("mode")]
    public string Mode { get; set; } = "disabled";

    /// <summary>
    /// Settings specific to Windows-native Python execution.
    /// </summary>
    [JsonPropertyName("windowsSettings")]
    public PythonScriptWindowsSettings WindowsSettings { get; set; } = new();

    /// <summary>
    /// Settings specific to WSL Python execution.
    /// </summary>
    [JsonPropertyName("wslSettings")]
    public PythonScriptWslSettings WslSettings { get; set; } = new();

    [JsonPropertyName("timeoutSeconds")]
    public int TimeoutSeconds { get; set; } = 30;

    [JsonPropertyName("value")]
    public List<AdvancedPastePythonScriptAction> Value { get; set; } = [];

    [JsonPropertyName("trustedScriptHashes")]
    public Dictionary<string, string> TrustedScriptHashes { get; set; } = [];

    // Legacy properties for backward compatibility during migration
    [JsonPropertyName("isEnabled")]
    public bool? IsEnabled { get; set; }

    [JsonPropertyName("useWsl")]
    public bool? UseWsl { get; set; }

    [JsonPropertyName("scriptsFolder")]
    public string ScriptsFolder { get; set; }

    [JsonPropertyName("pythonExecutablePath")]
    public string PythonExecutablePath { get; set; }

    [JsonPropertyName("wslDistribution")]
    public string WslDistribution { get; set; }

    /// <summary>
    /// Migrates legacy settings (isEnabled/useWsl) to new mode format on first load.
    /// </summary>
    public void MigrateLegacyIfNeeded()
    {
        if (IsEnabled.HasValue)
        {
            // Migrate from old format
            if (!IsEnabled.Value)
            {
                Mode = "disabled";
            }
            else if (UseWsl == true)
            {
                Mode = "wsl";
            }
            else
            {
                Mode = "windows";
            }

            if (!string.IsNullOrEmpty(ScriptsFolder))
            {
                WindowsSettings.ScriptsFolder = ScriptsFolder;
            }

            if (!string.IsNullOrEmpty(PythonExecutablePath))
            {
                WindowsSettings.PythonExecutablePath = PythonExecutablePath;
            }

            if (!string.IsNullOrEmpty(WslDistribution))
            {
                WslSettings.Distribution = WslDistribution;
            }

            // Clear legacy fields
            IsEnabled = null;
            UseWsl = null;
            ScriptsFolder = null;
            PythonExecutablePath = null;
            WslDistribution = null;
        }
    }
}
