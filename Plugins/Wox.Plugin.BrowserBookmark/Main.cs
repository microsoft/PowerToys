using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using Wox.Infrastructure.Storage;
using Wox.Plugin.BrowserBookmark.Commands;
using Wox.Plugin.BrowserBookmark.Models;
using Wox.Plugin.BrowserBookmark.Views;
using Wox.Plugin.SharedCommands;

namespace Wox.Plugin.BrowserBookmark
{
    public class Main : ISettingProvider, IPlugin, IReloadable, IPluginI18n, ISavable
    {
        private PluginInitContext context;
        
        private List<Bookmark> cachedBookmarks = new List<Bookmark>();

        private readonly Settings _settings;
        private readonly PluginJsonStorage<Settings> _storage;

        public Main()
        {
            _storage = new PluginJsonStorage<Settings>();
            _settings = _storage.Load();

            cachedBookmarks = Bookmarks.LoadAllBookmarks();
        }

        public void Init(PluginInitContext context)
        {
            this.context = context;
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
                SubTitle = c.Url,
                IcoPath = @"Images\bookmark.png",
                Score = 5,
                Action = (e) =>
                {
                    if (_settings.OpenInNewBrowserWindow)
                    {
                        c.Url.NewBrowserWindow("");
                    }
                    else
                    {
                        c.Url.NewTabInBrowser("");
                    }

                    return true;
                }
            }).ToList();
        }

        public void ReloadData()
        {
            cachedBookmarks.Clear();

            cachedBookmarks = Bookmarks.LoadAllBookmarks();
        }

        public string GetTranslatedPluginTitle()
        {
            return context.API.GetTranslation("wox_plugin_browserbookmark_plugin_name");
        }

        public string GetTranslatedPluginDescription()
        {
            return context.API.GetTranslation("wox_plugin_browserbookmark_plugin_description");
        }

        public Control CreateSettingPanel()
        {
            return new SettingsControl(_settings);
        }

        public void Save()
        {
            _storage.Save();
        }
    }
}
