using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Wox.Infrastructure.Storage;

namespace Wox.Plugin.FindFile
{
    public class FindFileContextMenuStorage : BaseStorage<FindFileContextMenuStorage>
    {
        [JsonProperty]
        public List<ContextMenu> ContextMenus = new List<ContextMenu>();

        protected override string ConfigName
        {
            get { return "FindFileContextMenu"; }
        }

        protected override FindFileContextMenuStorage LoadDefaultConfig()
        {
            ContextMenus = new List<ContextMenu>()
            {
                new ContextMenu()
                {
                    Name = "Open Containing Folder",
                    Command = "explorer.exe",
                    Argument = " /select \"{path}\""
                }
            };
            return this;
        }
    }

    public class ContextMenu
    {
        [JsonProperty]
        public string Name { get; set; }

        [JsonProperty]
        public string Command { get; set; }

        [JsonProperty]
        public string Argument { get; set; }
    }
}
