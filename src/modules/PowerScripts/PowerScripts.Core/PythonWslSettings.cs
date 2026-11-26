// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using PowerScripts.Core.Storage;

namespace PowerScripts.Core;

/// <summary>WSL-mode Python settings.</summary>
public sealed class PythonWslSettings
{
    /// <summary>Gets or sets the WSL distribution to run scripts in; empty means the default distribution.</summary>
    [JsonPropertyName("distribution")]
    public string Distribution { get; set; } = string.Empty;

    /// <summary>Gets or sets the user-provided managed Linux image; required when MXC is enabled.</summary>
    [JsonPropertyName("containerImage")]
    public string ContainerImage { get; set; } = string.Empty;
}
