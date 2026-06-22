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

    // Legacy properties — read for migration, never written back
    [JsonPropertyName("isEnabled")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IsEnabled { get; set; }

    [JsonPropertyName("useWsl")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? UseWsl { get; set; }

    [JsonPropertyName("scriptsFolder")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string ScriptsFolder { get; set; }

    [JsonPropertyName("pythonExecutablePath")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string PythonExecutablePath { get; set; }

    [JsonPropertyName("wslDistribution")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string WslDistribution { get; set; }

    /// <summary>
    /// Migrates legacy settings (isEnabled/useWsl) to new mode format on first load.
    /// </summary>
    public void MigrateLegacyIfNeeded()
    {
        // Only migrate if Mode hasn't been set by the new UI yet
        // (i.e., still at default "disabled") AND legacy fields are present.
        if (IsEnabled.HasValue && string.Equals(Mode, "disabled", System.StringComparison.OrdinalIgnoreCase))
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
        }

        // Always clear legacy fields so they don't persist
        IsEnabled = null;
        UseWsl = null;
        ScriptsFolder = null;
        PythonExecutablePath = null;
        WslDistribution = null;
    }
}
