using System.IO;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Wox.Plugin.WebSearch
{
    public class WebSearch
    {
        public const string DefaultIcon = "web_search.png";
        public string Title { get; set; }
        public string ActionKeyword { get; set; }
        [NotNull]
        private string _icon = DefaultIcon;

        [NotNull]
        public string Icon
        {
            get { return _icon; }
            set
            {
                _icon = value;
                IconPath = Path.Combine(WebSearchPlugin.PluginDirectory, WebSearchPlugin.ImageDirectory, value);
            }
        }

        /// <summary>
        /// All icon should be put under Images directory
        /// </summary>
        [NotNull]
        [JsonIgnore]
        internal string IconPath { get; private set; } = Path.Combine
        (
            WebSearchPlugin.PluginDirectory, WebSearchPlugin.ImageDirectory, DefaultIcon
        );

        public string Url { get; set; }
        public bool Enabled { get; set; }
    }
}