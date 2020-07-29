using System.Collections.Generic;
using Newtonsoft.Json;
using Wox.Infrastructure.Storage;

namespace Microsoft.Plugin.Folder
{
    public class FolderSettings
    {
        [JsonProperty]
        public List<FolderLink> FolderLinks { get;} = new List<FolderLink>();
    }
}
