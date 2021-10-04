// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Text.Json.Serialization;

namespace Microsoft.Plugin.Folder
{
    public class FolderLink
    {
        public string Path { get; set; }

        [JsonIgnore]
        public string Nickname =>
           Path.Split(new[] { System.IO.Path.DirectorySeparatorChar }, StringSplitOptions.None)
               .Last()
           + " (" + System.IO.Path.GetDirectoryName(Path) + ")";
    }
}
