// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace Community.PowerToys.Run.Plugin.VSCodeWorkspaces.WorkspacesHelper
{
    public class VSCodeWorkspaceEntry
    {
        [JsonPropertyName("folderUri")]
        public string FolderUri { get; set; }

        [JsonPropertyName("label")]
        public string Label { get; set; }

        [JsonPropertyName("remoteAuthority")]
        public string RemoteAuthority { get; set; }

        [JsonPropertyName("workspace")]
        public VSCodeWorkspaceProperty Workspace { get; set; }
    }
}
