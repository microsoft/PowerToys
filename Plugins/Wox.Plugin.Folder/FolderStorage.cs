using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using Wox.Infrastructure.Storage;
using System.IO;

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
