// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace PowerScripts.Core.Manifest;

/// <summary>Display-only labels for a boolean choice; values remain JSON booleans.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class ParameterUiHints
{
    [JsonPropertyName("control")]
    public string Control { get; init; } = string.Empty;

    [JsonPropertyName("trueLabel")]
    public string TrueLabel { get; init; } = string.Empty;

    [JsonPropertyName("falseLabel")]
    public string FalseLabel { get; init; } = string.Empty;
}
