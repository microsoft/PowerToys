using System.Collections.Generic;
using Newtonsoft.Json;
using Wox.Infrastructure.Storage;

namespace Microsoft.Plugin.Folder
{
    public class FolderSettings
    {
        [JsonProperty]
        public List<FolderLink> FolderLinks { get;} = new List<FolderLink>();

        [JsonProperty]
        public int MaxFolderResults { get; set; } = 50;

        [JsonProperty]
        public int MaxFileResults { get; set; } = 50;

    }
}
