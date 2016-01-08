using System.Collections.Generic;
using Newtonsoft.Json;
using Wox.Infrastructure.Storage;

namespace Wox.Plugin.Folder
{
    public class FolderStorage : JsonStrorage<FolderStorage>
    {
        [JsonProperty]
        public List<FolderLink> FolderLinks { get; set; }

        protected override string FileName { get; } = "settings_folder_plugin";
    }
}
