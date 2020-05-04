using System.Collections.Generic;
using System.Linq;
using Microsoft.Plugin.WindowWalker.Components;
using Wox.Plugin;

namespace Microsoft.Plugin.WindowWalker
{
    public class Main : IPlugin, IPluginI18n
    {
        private static List<SearchResult> _results = new List<SearchResult>();
        private static string IcoPath = "Images/windowwalker.png";
        private PluginInitContext Context { get; set; }

        static Main()
        {
            SearchController.Instance.OnSearchResultUpdate += SearchResultUpdated;
            OpenWindows.Instance.UpdateOpenWindowsList();
        }

        public List<Result> Query(Query query)
        {
            SearchController.Instance.UpdateSearchText(query.RawQuery).Wait();
            OpenWindows.Instance.UpdateOpenWindowsList();
            return _results.Select(x => new Result()
            {
                Title = x.Result.Title,
                IcoPath = IcoPath,
                SubTitle = "Running: " + x.Result.ProcessName,
                Action = c =>
                {
                    x.Result.SwitchToWindow();
                    return true;
                }
            }
            ).ToList();
        }

        public void Init(PluginInitContext context)
        {
            Context = context;
        }

        public string GetTranslatedPluginTitle()
        {
            return Context.API.GetTranslation("wox_plugin_windowwalker_plugin_name");
        }

        public string GetTranslatedPluginDescription()
        {
            return Context.API.GetTranslation("wox_plugin_windowwalker_plugin_description");
        }

        private static void SearchResultUpdated(object sender, SearchController.SearchResultUpdateEventArgs e)
        {
            _results = SearchController.Instance.SearchMatches;
        }
    }
}
