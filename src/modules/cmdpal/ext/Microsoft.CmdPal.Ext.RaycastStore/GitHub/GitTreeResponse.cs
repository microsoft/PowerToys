// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.CmdPal.Ext.RaycastStore.GitHub;

internal sealed class GitTreeResponse
{
    [JsonPropertyName("sha")]
    public string? Sha { get; set; }

    [JsonPropertyName("tree")]
    public List<GitTreeEntry>? Tree { get; set; }

    [JsonPropertyName("truncated")]
    public bool Truncated { get; set; }
}
