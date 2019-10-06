using System.Collections.Generic;
using System.Linq;
using Wox.Plugin.BrowserBookmark.Commands;
using Wox.Plugin.SharedCommands;

namespace Wox.Plugin.BrowserBookmark
{
    public class Main : IPlugin, IReloadable
    {
        private PluginInitContext context;
        
        private List<Bookmark> cachedBookmarks = new List<Bookmark>(); 

        public void Init(PluginInitContext context)
        {
            this.context = context;

            cachedBookmarks = Bookmarks.LoadAllBookmarks();
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
                returnList = cachedBookmarks.Where(o => Bookmarks.MatchProgram(o, param)).ToList();
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

        public void ReloadData()
        {
            cachedBookmarks.Clear();

            cachedBookmarks = Bookmarks.LoadAllBookmarks();
        }
    }
}
