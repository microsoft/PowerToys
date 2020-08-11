// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using Newtonsoft.Json;

namespace Microsoft.Plugin.Folder
{
    [JsonObject(MemberSerialization.OptIn)]
    public class FolderLink
    {
        [JsonProperty]
        public string Path { get; set; }

        public string Nickname =>
           Path.Split(new[] { System.IO.Path.DirectorySeparatorChar }, StringSplitOptions.None)
               .Last()
           + " (" + System.IO.Path.GetDirectoryName(Path) + ")";
    }
}
