// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace LanguageModelProvider.FoundryLocal;

internal sealed class FoundryDownloadBody
{
    [JsonPropertyName("Model")]
    public required FoundryModelDownload Model { get; init; }

    [JsonPropertyName("token")]
    public required string Token { get; init; }

    [JsonPropertyName("IgnorePipeReport")]
    public required bool IgnorePipeReport { get; init; }
}
