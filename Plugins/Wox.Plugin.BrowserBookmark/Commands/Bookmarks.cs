using System.Collections.Generic;
using System.Linq;
using Wox.Infrastructure;

namespace Wox.Plugin.BrowserBookmark.Commands
{
    internal static class Bookmarks
    {
        internal static bool MatchProgram(Bookmark bookmark, string queryString)
        {
            if (StringMatcher.FuzzySearch(queryString, bookmark.Name, new MatchOption()).IsSearchPrecisionScoreMet()) return true;
            if (StringMatcher.FuzzySearch(queryString, bookmark.PinyinName, new MatchOption()).IsSearchPrecisionScoreMet()) return true;
            if (StringMatcher.FuzzySearch(queryString, bookmark.Url, new MatchOption()).IsSearchPrecisionScoreMet()) return true;

            return false;
        }

        internal static List<Bookmark> LoadAllBookmarks()
        {
            var allbookmarks = new List<Bookmark>();
            
            var chromeBookmarks = new ChromeBookmarks();
            var mozBookmarks = new FirefoxBookmarks();

            //TODO: Let the user select which browser's bookmarks are displayed
            // Add Firefox bookmarks
            mozBookmarks.GetBookmarks().ForEach(x => allbookmarks.Add(x));

            // Add Chrome bookmarks
            chromeBookmarks.GetBookmarks().ForEach(x => allbookmarks.Add(x));

            return allbookmarks.Distinct().ToList();
        }
    }
}
