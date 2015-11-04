using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using Wox.Infrastructure.Storage;

namespace Wox.UpdateFeedGenerator
{
    public class ConfigStorage : JsonStrorage<ConfigStorage>
    {
        [JsonProperty]
        public string OutputDirectory { get; set; }

        [JsonProperty]
        public string SourceDirectory { get; set; }

        [JsonProperty]
        public string BaseURL { get; set; }

        [JsonProperty]
        public string FeedXMLName { get; set; }

        [JsonProperty]
        public bool CheckVersion { get; set; }

        [JsonProperty]
        public bool CheckSize { get; set; }

        [JsonProperty]
        public bool CheckDate { get; set; }

        [JsonProperty]
        public bool CheckHash { get; set; }

        protected override void OnAfterLoad(ConfigStorage config)
        {
            if (string.IsNullOrEmpty(config.OutputDirectory))
            {
                config.OutputDirectory = @"Update";
                ConfigStorage.Instance.Save();
            }
        }

        protected override string ConfigFolder
        {
            get { return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location); }
        }

        protected override string ConfigName
        {
            get { return "config"; }
        }
    }
}
