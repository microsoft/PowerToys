using System.Collections.Generic;
using System.Linq;
using Wox.Infrastructure;
using Wox.Plugin.SharedCommands;

namespace Wox.Plugin.BrowserBookmark
{
    public class Main : IPlugin
    {
        private PluginInitContext context;

        // TODO: periodically refresh the Cache?
        private List<Bookmark> cachedBookmarks = new List<Bookmark>(); 

        public void Init(PluginInitContext context)
        {
            this.context = context;

            // Cache all bookmarks
            var chromeBookmarks = new ChromeBookmarks();
            var mozBookmarks = new FirefoxBookmarks();

            //TODO: Let the user select which browser's bookmarks are displayed
            // Add Firefox bookmarks
            cachedBookmarks.AddRange(mozBookmarks.GetBookmarks());
            // Add Chrome bookmarks
            cachedBookmarks.AddRange(chromeBookmarks.GetBookmarks());

            cachedBookmarks = cachedBookmarks.Distinct().ToList();
        }

        public List<Result> Query(Query query)
        {
            string param = query.GetAllRemainingParameter().TrimStart();

            // Should top results be returned? (true if no search parameters have been passed)
            var topResults = string.IsNullOrEmpty(param);
            
            var returnList = cachedBookmarks;

            if (!topResults)
            {
                // Since we mixed chrome and firefox bookmarks, we should order them again                
                returnList = cachedBookmarks.Where(o => MatchProgram(o, param)).ToList();
                returnList = returnList.OrderByDescending(o => o.Score).ToList();
            }
            
            return returnList.Select(c => new Result()
            {
                Title = c.Name,
                SubTitle = "Bookmark: " + c.Url,
                IcoPath = @"Images\bookmark.png",
                Score = 5,
                Action = (e) =>
                {
                    context.API.HideApp();
                    c.Url.NewBrowserWindow("");
                    return true;
                }
            }).ToList();
        }

        private bool MatchProgram(Bookmark bookmark, string queryString)
        {
            if (StringMatcher.FuzzySearch(queryString, bookmark.Name, new MatchOption()).IsSearchPrecisionScoreMet()) return true;
            if ( StringMatcher.FuzzySearch(queryString, bookmark.PinyinName, new MatchOption()).IsSearchPrecisionScoreMet()) return true;
            if (StringMatcher.FuzzySearch(queryString, bookmark.Url, new MatchOption()).IsSearchPrecisionScoreMet()) return true;

            return false;
        }
    }
}
