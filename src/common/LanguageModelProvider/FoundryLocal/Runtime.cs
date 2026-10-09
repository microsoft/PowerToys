// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace LanguageModelProvider.FoundryLocal;

internal sealed record Runtime
{
    [JsonPropertyName("deviceType")]
    public string DeviceType { get; init; } = string.Empty;

    [JsonPropertyName("executionProvider")]
    public string ExecutionProvider { get; init; } = string.Empty;
}
