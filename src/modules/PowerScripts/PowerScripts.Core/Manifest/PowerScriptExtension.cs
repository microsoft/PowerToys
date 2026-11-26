// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace PowerScripts.Core.Manifest;

/// <summary>
/// The <c>x-powerscript</c> block: the small, PowerToys-specific residue that neither MCP nor a
/// language signature can express. Everything here is optional.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class PowerScriptExtension
{
    /// <summary>Gets or sets declared capabilities: the consent string and the agent permission contract.</summary>
    [JsonPropertyName("capabilities")]
    public List<string>? Capabilities { get; set; }

    [JsonPropertyName("hostCli")]
    public PowerScripts.Core.Cli.HostCliPermission? HostCli { get; set; }

    [JsonPropertyName("publisher")]
    public string? Publisher { get; set; }

    [JsonPropertyName("version")]
    public string? Version { get; set; }

    [JsonPropertyName("source")]
    public string? Source { get; set; }

    [JsonPropertyName("icon")]
    public string? Icon { get; set; }
}
