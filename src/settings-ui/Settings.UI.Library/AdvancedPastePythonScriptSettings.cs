// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Library;

public sealed class AdvancedPastePythonScriptSettings
{
    [JsonPropertyName("scriptsFolder")]
    public string ScriptsFolder { get; set; } = string.Empty;

    [JsonPropertyName("pythonExecutablePath")]
    public string PythonExecutablePath { get; set; } = string.Empty;

    [JsonPropertyName("timeoutSeconds")]
    public int TimeoutSeconds { get; set; } = 30;

    [JsonPropertyName("value")]
    public List<AdvancedPastePythonScriptAction> Value { get; set; } = [];

    [JsonPropertyName("trustedScriptHashes")]
    public Dictionary<string, string> TrustedScriptHashes { get; set; } = [];
}
