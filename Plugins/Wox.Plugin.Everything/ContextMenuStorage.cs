using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Exceptionless.Json;
using JsonProperty = Newtonsoft.Json.JsonPropertyAttribute;
using Wox.Infrastructure.Storage;

namespace Wox.Plugin.Everything
{
    public class ContextMenuStorage : JsonStrorage<ContextMenuStorage>
    {
        [JsonProperty] public List<ContextMenu> ContextMenus = new List<ContextMenu>();

        [JsonProperty]
        public int MaxSearchCount { get; set; }

        public IPublicAPI API { get; set; }

        protected override string ConfigName
        {
            get { return "EverythingContextMenu"; }
        }

        protected override string ConfigFolder
        {
            get { return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location); }
        }

        protected override void OnAfterLoad(ContextMenuStorage obj)
        {
           
        }

        protected override ContextMenuStorage LoadDefault()
        {
            MaxSearchCount = 100;
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