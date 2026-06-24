// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace LanguageModelProvider.FoundryLocal;

internal sealed record PromptTemplate
{
    [JsonPropertyName("assistant")]
    public string Assistant { get; init; } = string.Empty;

    [JsonPropertyName("prompt")]
    public string Prompt { get; init; } = string.Empty;
}
