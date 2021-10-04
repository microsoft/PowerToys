// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Community.PowerToys.Run.Plugin.VSCodeWorkspaces.WorkspacesHelper
{
    public class OpenedPathsList
    {
        [JsonPropertyName("workspaces3")]
        public List<dynamic> Workspaces3 { get; set; }

        [JsonPropertyName("entries")]
        public List<VSCodeWorkspaceEntry> Entries { get; set; }
    }
}
