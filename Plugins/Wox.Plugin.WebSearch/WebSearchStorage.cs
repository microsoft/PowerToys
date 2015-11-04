using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using Wox.Infrastructure.Storage;

namespace Wox.Plugin.WebSearch
{
    public class WebSearchStorage :JsonStrorage<WebSearchStorage>
    {
        [JsonProperty]
        public List<WebSearch> WebSearches { get; set; }

        [JsonProperty]
        public bool EnableWebSearchSuggestion { get; set; }

        [JsonProperty]
        public string WebSearchSuggestionSource { get; set; }

        protected override string ConfigFolder
        {
            get { return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location); }
        }

        protected override string ConfigName
        {
            get { return "setting"; }
        }

        protected override WebSearchStorage LoadDefault()
        {
            WebSearches = LoadDefaultWebSearches();
            return this;
        }

        public List<WebSearch> LoadDefaultWebSearches()
        {
            List<WebSearch> webSearches = new List<WebSearch>();

            WebSearch googleWebSearch = new WebSearch()
            {
                Title = "Google",
                ActionKeyword = "g",
                IconPath = @"Images\websearch\google.png",
                Url = "https://www.google.com/search?q={q}",
                Enabled = true
            };
            webSearches.Add(googleWebSearch);


            WebSearch wikiWebSearch = new WebSearch()
            {
                Title = "Wikipedia",
                ActionKeyword = "wiki",
                IconPath = @"Images\websearch\wiki.png",
                Url = "http://en.wikipedia.org/wiki/{q}",
                Enabled = true
            };
            webSearches.Add(wikiWebSearch);

            WebSearch findIcon = new WebSearch()
            {
                Title = "FindIcon",
                ActionKeyword = "findicon",
                IconPath = @"Images\websearch\pictures.png",
                Url = "http://findicons.com/search/{q}",
                Enabled = true
            };
            webSearches.Add(findIcon);

            return webSearches;
        }
    }
}
