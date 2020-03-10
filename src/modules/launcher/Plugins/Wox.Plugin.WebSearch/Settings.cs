using System;
using System.Collections.ObjectModel;
using Newtonsoft.Json;
using Wox.Plugin.WebSearch.SuggestionSources;

namespace Wox.Plugin.WebSearch
{
    public class Settings : BaseModel
    {
        public Settings()
        {
            SelectedSuggestion = Suggestions[0];
            if (SearchSources.Count > 0)
            {
                SelectedSearchSource = SearchSources[0];
            }
        }

        public ObservableCollection<SearchSource> SearchSources { get; set; } = new ObservableCollection<SearchSource>
        {
            new SearchSource
            {
                Title = "Google",
                ActionKeyword = "g",
                Icon = "google.png",
                Url = "https://www.google.com/search?q={q}",
                Enabled = true
            },
            new SearchSource
            {
                Title = "Google Scholar",
                ActionKeyword = "sc",
                Icon = "google.png",
                Url = "https://scholar.google.com/scholar?q={q}",
                Enabled = true
            },
            new SearchSource
            {
                Title = "Wikipedia",
                ActionKeyword = "wiki",
                Icon = "wiki.png",
                Url = "https://en.wikipedia.org/wiki/{q}",
                Enabled = true
            },
            new SearchSource
            {
                Title = "FindIcon",
                ActionKeyword = "findicon",
                Icon = "pictures.png",
                Url = "http://findicons.com/search/{q}",
                Enabled = true
            },
            new SearchSource
            {
                Title = "Facebook",
                ActionKeyword = "facebook",
                Icon = "facebook.png",
                Url = "https://www.facebook.com/search/?q={q}",
                Enabled = true
            },
            new SearchSource
            {
                Title = "Twitter",
                ActionKeyword = "twitter",
                Icon = "twitter.png",
                Url = "https://twitter.com/search?q={q}",
                Enabled = true
            },
            new SearchSource
            {
                Title = "Google Maps",
                ActionKeyword = "maps",
                Icon = "google_maps.png",
                Url = "https://maps.google.com/maps?q={q}",
                Enabled = true
            },
            new SearchSource
            {
                Title = "Google Translate",
                ActionKeyword = "translate",
                Icon = "google_translate.png",
                Url = "https://translate.google.com/#auto|en|{q}",
                Enabled = true
            },
            new SearchSource
            {
                Title = "Duckduckgo",
                ActionKeyword = "duckduckgo",
                Icon = "duckduckgo.png",
                Url = "https://duckduckgo.com/?q={q}",
                Enabled = true
            },
            new SearchSource
            {
                Title = "Github",
                ActionKeyword = "github",
                Icon = "github.png",
                Url = "https://github.com/search?q={q}",
                Enabled = true
            },
            new SearchSource
            {
                Title = "Github Gist",
                ActionKeyword = "gist",
                Icon = "gist.png",
                Url = "https://gist.github.com/search?q={q}",
                Enabled = true
            },
            new SearchSource
            {
                Title = "Gmail",
                ActionKeyword = "gmail",
                Icon = "gmail.png",
                Url = "https://mail.google.com/mail/ca/u/0/#apps/{q}",
                Enabled = true
            },
            new SearchSource
            {
                Title = "Google Drive",
                ActionKeyword = "drive",
                Icon = "google_drive.png",
                Url = "https://drive.google.com/?hl=en&tab=bo#search/{q}",
                Enabled = true
            },
            new SearchSource
            {
                Title = "Wolframalpha",
                ActionKeyword = "wolframalpha",
                Icon = "wolframalpha.png",
                Url = "https://www.wolframalpha.com/input/?i={q}",
                Enabled = true
            },
            new SearchSource
            {
                Title = "Stackoverflow",
                ActionKeyword = "stackoverflow",
                Icon = "stackoverflow.png",
                Url = "https://stackoverflow.com/search?q={q}",
                Enabled = true
            },
            new SearchSource
            {
                Title = "I'm Feeling Lucky",
                ActionKeyword = "lucky",
                Icon = "google.png",
                Url = "https://google.com/search?q={q}&btnI=I",
                Enabled = true
            },
            new SearchSource
            {
                Title = "Google Image",
                ActionKeyword = "image",
                Icon = "google.png",
                Url = "https://www.google.com/search?q={q}&tbm=isch",
                Enabled = true
            },
            new SearchSource
            {
                Title = "Youtube",
                ActionKeyword = "youtube",
                Icon = "youtube.png",
                Url = "https://www.youtube.com/results?search_query={q}",
                Enabled = true
            },
            new SearchSource
            {
                Title = "Bing",
                ActionKeyword = "bing",
                Icon = "bing.png",
                Url = "https://www.bing.com/search?q={q}",
                Enabled = true
            },
            new SearchSource
            {
                Title = "Yahoo",
                ActionKeyword = "yahoo",
                Icon = "yahoo.png",
                Url = "https://www.search.yahoo.com/search?p={q}",
                Enabled = true
            },
            new SearchSource
            {
                Title = "Baidu",
                ActionKeyword = "bd",
                Icon = "baidu.png",
                Url = "https://www.baidu.com/#ie=UTF-8&wd={q}",
                Enabled = true
            }
        };

        [JsonIgnore]
        public SearchSource SelectedSearchSource { get; set; }

        public bool EnableSuggestion { get; set; }

        [JsonIgnore]
        public SuggestionSource[] Suggestions { get; set; } = {
            new Google(),
            new Baidu()
        };

        [JsonIgnore]
        public SuggestionSource SelectedSuggestion { get; set; }

        /// <summary>
        /// used to store Settings.json only
        /// </summary>
        public string Suggestion
        {
            get { return SelectedSuggestion.ToString(); }
            set
            {
                foreach (var s in Suggestions)
                {
                    if (string.Equals(s.ToString(), value, StringComparison.OrdinalIgnoreCase))
                    {
                        SelectedSuggestion = s;
                    }
                }
            }
        }

        public string BrowserPath { get; set; }

        public bool OpenInNewBrowser { get; set; } = true;
    }
}