// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace PowerToys.WorkspacesMCP.Services;

internal sealed record ResourceReadParams
{
    [JsonPropertyName("uri")]
    public string Uri { get; init; } = string.Empty;
}
