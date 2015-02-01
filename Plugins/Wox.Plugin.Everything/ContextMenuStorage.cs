using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Wox.Infrastructure.Storage;
using System.IO;
using System.Reflection;

namespace Wox.Plugin.Everything
{
    public class ContextMenuStorage : JsonStrorage<ContextMenuStorage>
    {
        [JsonProperty]
        public List<ContextMenu> ContextMenus = new List<ContextMenu>();


        [JsonProperty]
        public int MaxSearchCount { get; set; }

        protected override string ConfigName
        {
            get { return "EverythingContextMenu"; }
        }

        protected override string ConfigFolder
        {
            get { return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location); }
        }

        protected override ContextMenuStorage LoadDefault()
        {
            ContextMenus = new List<ContextMenu>()
            {
                new ContextMenu()
                {
                    Name = "Open Containing Folder",
                    Command = "explorer.exe",
                    Argument = " /select,\"{path}\"",
                    ImagePath ="Images\\folder.png"
                }
            };
            MaxSearchCount = 100;
            Save();
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

        [JsonProperty]
        public string ImagePath { get; set; }
    }
}
