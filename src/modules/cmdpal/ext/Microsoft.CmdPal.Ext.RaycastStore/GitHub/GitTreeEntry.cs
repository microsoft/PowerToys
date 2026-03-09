// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace Microsoft.CmdPal.Ext.RaycastStore.GitHub;

internal sealed class GitTreeEntry
{
    [JsonPropertyName("path")]
    public string? Path { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("sha")]
    public string? Sha { get; set; }
}
