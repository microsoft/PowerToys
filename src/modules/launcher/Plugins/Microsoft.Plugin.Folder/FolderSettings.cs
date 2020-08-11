// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Newtonsoft.Json;

namespace Microsoft.Plugin.Folder
{
    public class FolderSettings
    {
        [JsonProperty]
        public List<FolderLink> FolderLinks { get; } = new List<FolderLink>();

        [JsonProperty]
        public int MaxFolderResults { get; set; } = 50;

        [JsonProperty]
        public int MaxFileResults { get; set; } = 50;
    }
}
