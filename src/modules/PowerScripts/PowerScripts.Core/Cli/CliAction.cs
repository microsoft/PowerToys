// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace PowerScripts.Core.Cli;

/// <summary>The editable entry data included in a PowerToys-operation script's trust fingerprint.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class CliAction
{
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; init; }

    [JsonPropertyName("commandId")]
    public string CommandId { get; init; } = string.Empty;

    [JsonPropertyName("bindings")]
    public Dictionary<string, CliActionBinding> Bindings { get; init; } = new(StringComparer.Ordinal);
}
