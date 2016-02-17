using System.Collections.Generic;
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

        protected override WebSearchStorage LoadDefault()
        {
            WebSearches = new List<WebSearch>(new List<WebSearch>()
            {
                new WebSearch
                {
                    Title = "Google",
                    ActionKeyword = "g",
                    IconPath = "Images\\google.png",
                    Url = "https://www.google.com/search?q={q}",
                    Enabled = true
                },
                new WebSearch
                {
                    Title = "Wikipedia",
                    ActionKeyword = "wiki",
                    IconPath = "Images\\wiki.png",
                    Url = "http://en.wikipedia.org/wiki/{q}",
                    Enabled = true
                },
                new WebSearch
                {
                    Title = "FindIcon",
                    ActionKeyword = "findicon",
                    IconPath = "Images\\pictures.png",
                    Url = "http://findicons.com/search/{q}",
                    Enabled = true
                },
                new WebSearch
                {
                    Title = "Facebook",
                    ActionKeyword = "facebook",
                    IconPath = "Images\\facebook.png",
                    Url = "http://www.facebook.com/search/?q={q}",
                    Enabled = true
                },
                new WebSearch
                {
                    Title = "Twitter",
                    ActionKeyword = "twitter",
                    IconPath = "Images\\twitter.png",
                    Url = "http://twitter.com/search?q={q}",
                    Enabled = true
                },
                new WebSearch
                {
                    Title = "Google Maps",
                    ActionKeyword = "maps",
                    IconPath = "Images\\google_maps.png",
                    Url = "http://maps.google.com/maps?q={q}",
                    Enabled = true
                },
                new WebSearch
                {
                    Title = "Google Translate",
                    ActionKeyword = "translate",
                    IconPath = "Images\\google_translate.png",
                    Url = "http://translate.google.com/#auto|en|{q}",
                    Enabled = true
                },
                new WebSearch
                {
                    Title = "Duckduckgo",
                    ActionKeyword = "duckduckgo",
                    IconPath = "Images\\duckduckgo.png",
                    Url = "https://duckduckgo.com/?q={q}",
                    Enabled = true
                },
                new WebSearch
                {
                    Title = "Github",
                    ActionKeyword = "github",
                    IconPath = "Images\\github.png",
                    Url = "https://github.com/search?q={q}",
                    Enabled = true
                },
                new WebSearch
                {
                    Title = "Github Gist",
                    ActionKeyword = "gist",
                    IconPath = "Images\\gist.png",
                    Url = "https://gist.github.com/search?q={q}",
                    Enabled = true
                },
                new WebSearch
                {
                    Title = "Gmail",
                    ActionKeyword = "gmail",
                    IconPath = "Images\\gmail.png",
                    Url = "https://mail.google.com/mail/ca/u/0/#apps/{q}",
                    Enabled = true
                },
                new WebSearch
                {
                    Title = "Google Drive",
                    ActionKeyword = "drive",
                    IconPath = "Images\\google_drive.png",
                    Url = "http://drive.google.com/?hl=en&tab=bo#search/{q}",
                    Enabled = true
                },
                new WebSearch
                {
                    Title = "Wolframalpha",
                    ActionKeyword = "wolframalpha",
                    IconPath = "Images\\wolframalpha.png",
                    Url = "http://www.wolframalpha.com/input/?i={q}",
                    Enabled = true
                },
                new WebSearch
                {
                    Title = "Stackoverflow",
                    ActionKeyword = "stackoverflow",
                    IconPath = "Images\\stackoverflow.png",
                    Url = "http://stackoverflow.com/search?q={q}",
                    Enabled = true
                },
                new WebSearch
                {
                    Title = "I'm Feeling Lucky",
                    ActionKeyword = "lucky",
                    IconPath = "Images\\google.png",
                    Url = "http://google.com/search?q={q}&btnI=I",
                    Enabled = true
                },
                new WebSearch
                {
                    Title = "Google Image",
                    ActionKeyword = "image",
                    IconPath = "Images\\google.png",
                    Url = "https://www.google.com/search?q={q}&tbm=isch",
                    Enabled = true
                },
                new WebSearch
                {
                    Title = "Youtube",
                    ActionKeyword = "youtube",
                    IconPath = "Images\\youtube.png",
                    Url = "http://www.youtube.com/results?search_query={q}",
                    Enabled = true
                },
                new WebSearch
                {
                    Title = "Bing",
                    ActionKeyword = "bing",
                    IconPath = "Images\\bing.png",
                    Url = "https://www.bing.com/search?q={q}",
                    Enabled = true
                },
                new WebSearch
                {
                    Title = "Yahoo",
                    ActionKeyword = "yahoo",
                    IconPath = "Images\\yahoo.png",
                    Url = "http://www.search.yahoo.com/search?p={q}",
                    Enabled = true
                }
            });

            return this;
        }
    }
}