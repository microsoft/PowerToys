// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace LanguageModelProvider.FoundryLocal;

internal sealed class FoundryModelDownload
{
    [JsonPropertyName("Name")]
    public required string Name { get; init; }

    [JsonPropertyName("Uri")]
    public required string Uri { get; init; }

    [JsonPropertyName("Publisher")]
    public required string Publisher { get; init; }

    [JsonPropertyName("ProviderType")]
    public required string ProviderType { get; init; }

    [JsonPropertyName("PromptTemplate")]
    public required PromptTemplate? PromptTemplate { get; init; }
}
