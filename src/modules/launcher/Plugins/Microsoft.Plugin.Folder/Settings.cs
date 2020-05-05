using System.Collections.Generic;
using Newtonsoft.Json;
using Wox.Infrastructure.Storage;

namespace Microsoft.Plugin.Folder
{
    public class Settings
    {
        [JsonProperty]
        public List<FolderLink> FolderLinks { get; set; } = new List<FolderLink>();
    }
}
