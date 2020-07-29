using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Plugin.Uri.UriHelper;
using Microsoft.PowerToys.Settings.UI.Lib;
using Wox.Infrastructure.Storage;
using Wox.Plugin;

namespace Microsoft.Plugin.Uri
{
    public class Main : IPlugin, IPluginI18n, IContextMenu, ISavable, IDisposable
    {
        private readonly ExtendedUriParser _uriParser;
        private readonly UriResolver _uriResolver;
        private PluginInitContext _context;
        private bool _disposed;
        private readonly PluginJsonStorage<UriSettings> _storage;
        private UriSettings _uriSettings;

        public Main()
        {
            _storage = new PluginJsonStorage<UriSettings>();
            _uriSettings = _storage.Load();
            _uriParser = new ExtendedUriParser();
            _uriResolver = new UriResolver();
        }

        public string IconPath { get; set; }

        public PluginInitContext Context { get; protected set; }

        public List<ContextMenuResult> LoadContextMenus(Result selectedResult)
        {
            return new List<ContextMenuResult>(0);
        }


        public List<Result> Query(Query query)
        {
            var results = new List<Result>();

            if (_uriParser.TryParse(query.Search, out var uriResult)
                && _uriResolver.IsValidHost(uriResult))
            {
                var uriResultString = uriResult.ToString();

                results.Add(new Result
                {
                    Title = uriResultString,
                    IcoPath = IconPath,
                    Action = action =>
                    {
                        Process.Start(new ProcessStartInfo(uriResultString)
                        {
                            UseShellExecute = true
                        });
                        return true;
                    }
                });
            }

            return results;
        }

        public void Init(PluginInitContext context)
        {
            Context = context;
            Context.API.ThemeChanged += OnThemeChanged;
            UpdateIconPath(Context.API.GetCurrentTheme());
        }


        public string GetTranslatedPluginTitle()
        {
            return "Url Handler";
        }

        public string GetTranslatedPluginDescription()
        {
            return "Handles urls";
        }

        public void Save()
        {
            _storage.Save();
        }

        public void UpdateSettings(PowerLauncherSettings settings)
        {
        }

        private void OnThemeChanged(Theme oldtheme, Theme newTheme)
        {
            UpdateIconPath(newTheme);
        }

        private void UpdateIconPath(Theme theme)
        {
            if (theme == Theme.Light || theme == Theme.HighContrastWhite)
            {
                IconPath = "Images/uri.light.png";
            }
            else
            {
                IconPath = "Images/uri.dark.png";
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                _context.API.ThemeChanged -= OnThemeChanged;
                _disposed = true;
            }
        }
    }
}
