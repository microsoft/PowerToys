// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Community.PowerToys.Run.Plugin.VSCodeWorkspaces.WorkspacesHelper
{
    // v1.64 uses AppData\Roaming\Code\Backups\workspaces
    public class VSCodeStorageWorkspaceFile
    {
        [JsonPropertyName("folderWorkspaceInfos")]
        public List<VSCodeWorkspaceEntry> FolderWorkspaceInfos { get; set; }
    }
}
