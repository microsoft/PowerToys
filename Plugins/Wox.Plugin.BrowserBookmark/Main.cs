using System.Collections.Generic;
using System.Linq;
using Wox.Infrastructure;

namespace Wox.Plugin.BrowserBookmark
{
    public class Main : IPlugin
    {
        private PluginInitContext context;

        private ChromeBookmarks chromeBookmarks = new ChromeBookmarks();
        private FirefoxBookmarks mozBookmarks = new FirefoxBookmarks();

        public void Init(PluginInitContext context)
        {
            this.context = context;
        }

        public List<Result> Query(Query query)
        {
            string param = query.GetAllRemainingParameter().TrimStart();

            // Should top results be returned? (true if no search parameters have been passed)
            var topResults = string.IsNullOrEmpty(param);

            var returnList = new List<Bookmark>();

            // Add Firefox bookmarks
            returnList.AddRange(mozBookmarks.GetBookmarks(param, topResults));
            // Add Chrome bookmarks
            returnList.AddRange(chromeBookmarks.GetBookmarks(param));

            if (!topResults)
            {
                // Since we mixed chrome and firefox bookmarks, we should order them again
                var fuzzyMatcher = FuzzyMatcher.Create(param);
                returnList = returnList.Where(o => MatchProgram(o, fuzzyMatcher)).ToList();
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
                    context.HideApp();
                    context.ShellRun(c.Url);
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
