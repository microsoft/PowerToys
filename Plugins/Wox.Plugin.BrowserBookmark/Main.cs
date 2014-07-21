using System.Collections.Generic;
using System.Linq;
using Wox.Infrastructure;

namespace Wox.Plugin.BrowserBookmark
{
    public class Main : IPlugin
    {
        private PluginInitContext context;

        // TODO: periodically refresh the cache?
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
                var fuzzyMatcher = FuzzyMatcher.Create(param);
                returnList = cachedBookmarks.Where(o => MatchProgram(o, fuzzyMatcher)).ToList();
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
                    context.API.ShellRun(c.Url);
                    return true;
                }
            }).ToList();
        }

        private bool MatchProgram(Bookmark bookmark, FuzzyMatcher matcher)
        {
            if ((bookmark.Score = matcher.Evaluate(bookmark.Name).Score) > 0) return true;
            if ((bookmark.Score = matcher.Evaluate(bookmark.PinyinName).Score) > 0) return true;
            if ((bookmark.Score = matcher.Evaluate(bookmark.Url).Score / 10) > 0) return true;

            return false;
        }
    }
}
