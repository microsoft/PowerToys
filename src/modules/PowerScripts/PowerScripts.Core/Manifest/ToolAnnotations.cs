// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace PowerScripts.Core.Manifest;

/// <summary>The subset of MCP tool annotations PowerScripts reads.</summary>
public sealed class ToolAnnotations
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("readOnlyHint")]
    public bool? ReadOnlyHint { get; set; }

    [JsonPropertyName("destructiveHint")]
    public bool? DestructiveHint { get; set; }
}
