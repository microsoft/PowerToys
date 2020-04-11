using System.Collections.Generic;
using System.Linq;
using Wox.Plugin.WindowWalker.Components;

namespace Wox.Plugin.WindowWalker
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
            SearchController.Instance.SearchText = query.RawQuery;
            OpenWindows.Instance.UpdateOpenWindowsList();
            return _results.Select(x => new Result()
            {
                Title = x.Result.Title,
                IcoPath = IcoPath,
                SubTitle = x.Result.ProcessName
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
