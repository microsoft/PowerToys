// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TopToolbar.Services.Workspaces
{
    internal sealed class WorkspaceDocument
    {
        [JsonPropertyName("workspaces")]
        public List<WorkspaceDefinition> Workspaces { get; set; } = new();
    }
}
