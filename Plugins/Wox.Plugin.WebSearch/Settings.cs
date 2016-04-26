using System.Collections.Generic;
using Newtonsoft.Json;
using Wox.Infrastructure.Storage;

namespace Wox.Plugin.WebSearch
{
    public class Settings
    {
        public List<WebSearch> WebSearches { get; set; } = new List<WebSearch>
            {
                new WebSearch
                {
                    Title = "Google",
                    ActionKeyword = "g",
                    IconPath = "google.png",
                    Url = "https://www.google.com/search?q={q}",
                    Enabled = true
                },
                new WebSearch
                {
                    Title = "Wikipedia",
                    ActionKeyword = "wiki",
                    IconPath = "wiki.png",
                    Url = "http://en.wikipedia.org/wiki/{q}",
                    Enabled = true
                },
                new WebSearch
                {
                    Title = "FindIcon",
                    ActionKeyword = "findicon",
                    IconPath = "pictures.png",
                    Url = "http://findicons.com/search/{q}",
                    Enabled = true
                },
                new WebSearch
                {
                    Title = "Facebook",
                    ActionKeyword = "facebook",
                    IconPath = "facebook.png",
                    Url = "http://www.facebook.com/search/?q={q}",
                    Enabled = true
                },
                new WebSearch
                {
                    Title = "Twitter",
                    ActionKeyword = "twitter",
                    IconPath = "twitter.png",
                    Url = "http://twitter.com/search?q={q}",
                    Enabled = true
                },
                new WebSearch
                {
                    Title = "Google Maps",
                    ActionKeyword = "maps",
                    IconPath = "google_maps.png",
                    Url = "http://maps.google.com/maps?q={q}",
                    Enabled = true
                },
                new WebSearch
                {
                    Title = "Google Translate",
                    ActionKeyword = "translate",
                    IconPath = "google_translate.png",
                    Url = "http://translate.google.com/#auto|en|{q}",
                    Enabled = true
                },
                new WebSearch
                {
                    Title = "Duckduckgo",
                    ActionKeyword = "duckduckgo",
                    IconPath = "duckduckgo.png",
                    Url = "https://duckduckgo.com/?q={q}",
                    Enabled = true
                },
                new WebSearch
                {
                    Title = "Github",
                    ActionKeyword = "github",
                    IconPath = "github.png",
                    Url = "https://github.com/search?q={q}",
                    Enabled = true
                },
                new WebSearch
                {
                    Title = "Github Gist",
                    ActionKeyword = "gist",
                    IconPath = "gist.png",
                    Url = "https://gist.github.com/search?q={q}",
                    Enabled = true
                },
                new WebSearch
                {
                    Title = "Gmail",
                    ActionKeyword = "gmail",
                    IconPath = "gmail.png",
                    Url = "https://mail.google.com/mail/ca/u/0/#apps/{q}",
                    Enabled = true
                },
                new WebSearch
                {
                    Title = "Google Drive",
                    ActionKeyword = "drive",
                    IconPath = "google_drive.png",
                    Url = "http://drive.google.com/?hl=en&tab=bo#search/{q}",
                    Enabled = true
                },
                new WebSearch
                {
                    Title = "Wolframalpha",
                    ActionKeyword = "wolframalpha",
                    IconPath = "wolframalpha.png",
                    Url = "http://www.wolframalpha.com/input/?i={q}",
                    Enabled = true
                },
                new WebSearch
                {
                    Title = "Stackoverflow",
                    ActionKeyword = "stackoverflow",
                    IconPath = "stackoverflow.png",
                    Url = "http://stackoverflow.com/search?q={q}",
                    Enabled = true
                },
                new WebSearch
                {
                    Title = "I'm Feeling Lucky",
                    ActionKeyword = "lucky",
                    IconPath = "google.png",
                    Url = "http://google.com/search?q={q}&btnI=I",
                    Enabled = true
                },
                new WebSearch
                {
                    Title = "Google Image",
                    ActionKeyword = "image",
                    IconPath = "google.png",
                    Url = "https://www.google.com/search?q={q}&tbm=isch",
                    Enabled = true
                },
                new WebSearch
                {
                    Title = "Youtube",
                    ActionKeyword = "youtube",
                    IconPath = "youtube.png",
                    Url = "http://www.youtube.com/results?search_query={q}",
                    Enabled = true
                },
                new WebSearch
                {
                    Title = "Bing",
                    ActionKeyword = "bing",
                    IconPath = "bing.png",
                    Url = "https://www.bing.com/search?q={q}",
                    Enabled = true
                },
                new WebSearch
                {
                    Title = "Yahoo",
                    ActionKeyword = "yahoo",
                    IconPath = "yahoo.png",
                    Url = "http://www.search.yahoo.com/search?p={q}",
                    Enabled = true
                }
            };

        public bool EnableWebSearchSuggestion { get; set; }

        public string WebSearchSuggestionSource { get; set; }
    }
}