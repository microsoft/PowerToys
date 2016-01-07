using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using Wox.Infrastructure.Storage;

namespace Wox.Plugin.WebSearch
{
    public class WebSearchStorage : JsonStrorage<WebSearchStorage>
    {
        [JsonProperty]
        public List<WebSearch> WebSearches { get; set; }

        [JsonProperty]
        public bool EnableWebSearchSuggestion { get; set; }

        [JsonProperty]
        public string WebSearchSuggestionSource { get; set; }

        protected override string FileName { get; } = "settings_plugin_websearch";
    }
}
