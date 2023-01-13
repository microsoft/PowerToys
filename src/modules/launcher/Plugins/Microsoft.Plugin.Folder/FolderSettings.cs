// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.Plugin.Folder
{
    public class FolderSettings
    {
        public List<FolderLink> FolderLinks { get; } = new List<FolderLink>();

        public int MaxFolderResults { get; set; } = 50;

        public int MaxFileResults { get; set; } = 50;
    }
}
