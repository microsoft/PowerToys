using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using Wox.Infrastructure.Storage;

namespace Wox.Plugin.Folder
{
    public class FolderStorage : JsonStrorage<FolderStorage>
    {
        [JsonProperty]
        public List<FolderLink> FolderLinks { get; set; }
        protected override string ConfigFolder
        {
            get { return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location); }
        }

        protected override string ConfigName
        {
            get { return "setting"; }
        }
    }
}
