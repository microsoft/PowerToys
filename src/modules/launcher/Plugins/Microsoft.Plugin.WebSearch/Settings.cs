using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using Newtonsoft.Json;
using Wox.Plugin.WebSearch.SuggestionSources;

namespace Wox.Plugin.WebSearch
{
    public class Settings : BaseModel
    {
        public Settings()
        {
            SelectedSuggestion = Suggestions["bingweb"];
            if (SearchSources.Count > 0)
            {
                SelectedSearchSource = SearchSources[0];
            }
        }

        public ObservableCollection<SearchSource> SearchSources { get; set; } = new ObservableCollection<SearchSource>
        {
            new SearchSource
            {
                Title = "Bing",
                ActionKeyword = "web",
                Icon = "bing.png",
                Url = "https://www.bing.com/search?q={q}",
                Enabled = true,
                SuggestionSources = "bingweb"
            },
            new SearchSource
            {
                Title = "Bing Dictionary",
                ActionKeyword = "dict",
                Icon = "bing.png",
                Url = "https://www.bing.com/search?q={q}",
                Enabled = true,
                SuggestionSources = "bingdict"
            },
            new SearchSource
            {
                Title = "Bing Map",
                ActionKeyword = "map",
                Icon = "bing.png",
                Url = "https://www.bing.com/maps?q={q}",
                Enabled = true,
                SuggestionSources = "bingmap"
            },
            new SearchSource
            {
                Title = "Bing Image",
                ActionKeyword = "image",
                Icon = "bing.png",
                Url = "https://www.bing.com/images/search/?q={q}",
                Enabled = true,
                SuggestionSources = "bingweb"
            }
        };

        [JsonIgnore]
        public SearchSource SelectedSearchSource { get; set; }

        public bool EnableSuggestion { get; set; } = true;

        public string locale { get; set; } = CultureInfo.CurrentCulture.Name;

        [JsonIgnore]
        public Dictionary<string, SuggestionSource> Suggestions { get; set; } = new Dictionary<string, SuggestionSource>(StringComparer.OrdinalIgnoreCase) {
            {"bingweb", new BingWeb()},
            {"bingdict", new BingDict()},
            {"bingmap", new BingMap()},
            {"google", new Google()},
            {"baidu", new Baidu()}
        };

        [JsonIgnore]
        public SuggestionSource SelectedSuggestion { get; set; }

        /// <summary>
        /// used to store Settings.json only
        /// </summary>
        public SuggestionSource GetSuggestion(string suggestionKey)
        {
            return Suggestions.TryGetValue(suggestionKey, out SuggestionSource sugg) ? sugg : SelectedSuggestion;
        }

        public string BrowserPath { get; set; }

        public bool OpenInNewBrowser { get; set; } = true;
    }
}