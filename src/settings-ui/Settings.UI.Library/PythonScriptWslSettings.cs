// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Library;

public sealed class PythonScriptWslSettings
{
    [JsonPropertyName("scriptsFolder")]
    public string ScriptsFolder { get; set; } = string.Empty;

    [JsonPropertyName("distribution")]
    public string Distribution { get; set; } = string.Empty;
}
