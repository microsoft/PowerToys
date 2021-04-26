using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using Community.PowerToys.Run.Plugin.Everything.Exception;
using Community.PowerToys.Run.Plugin.Everything.SearchHelper;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Wox.Infrastructure;
using Wox.Infrastructure.Storage;
using Wox.Plugin;
using Wox.Plugin.Logger;

namespace Community.PowerToys.Run.Plugin.Everything
{
    public class Main : ISettingProvider, IPlugin, ISavable, IPluginI18n, IContextMenu, IDisposable, IDelayedExecutionPlugin
    {
        private PluginJsonStorage<EverythingSettings> _storage;
        private PluginInitContext _context;
        private EverythingSettings _settings;
        private IContextMenu _contextMenuLoader;
        private bool disposedValue;
        private readonly EverythingApi _api = new EverythingApi();

        private string WarningIconPath { get; set; }

        public IEnumerable<PluginAdditionalOption> AdditionalOptions => new List<PluginAdditionalOption>();

        public string Name => Properties.Resources.Community_plugin_everything_plugin_name;

        public string Description => Properties.Resources.Community_plugin_everything_plugin_description;

        public Control CreateSettingPanel()
        {
            throw new NotImplementedException();
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        public string GetTranslatedPluginDescription()
        {
            return Properties.Resources.Community_plugin_everything_plugin_description;
        }

        public string GetTranslatedPluginTitle()
        {
            return Properties.Resources.Community_plugin_everything_plugin_name;
        }

        private void UpdateIconPath(Theme theme)
        {
            if (theme == Theme.Light || theme == Theme.HighContrastWhite)
            {
                WarningIconPath = "Images/Warning.light.png";
            }
            else
            {
                WarningIconPath = "Images/Warning.dark.png";
            }
        }

        private void OnThemeChanged(Theme currentTheme, Theme newTheme)
        {
            UpdateIconPath(newTheme);
        }

        public void Init(PluginInitContext context)
        {
            _context = context;
            _contextMenuLoader = new ContextMenuLoader(context);
            _storage = new PluginJsonStorage<EverythingSettings>();
            _settings = _storage.Load();
            _context.API.ThemeChanged += OnThemeChanged;
            UpdateIconPath(_context.API.GetCurrentTheme());
        }

        public List<ContextMenuResult> LoadContextMenus(Result selectedResult)
        {
            return _contextMenuLoader.LoadContextMenus(selectedResult);
        }

        public List<Result> Query(Query query)
        {
            return new List<Result>();
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "We want to keep the process alive but will log the exception")]
        public List<Result> Query(Query query, bool delayedExecution)
        {
            List<Result> results = new List<Result>();
            CancellationToken token = new CancellationToken();
            if (!string.IsNullOrEmpty(query.Search))
            {
                var searchQuery = query.Search;
                if (_settings.MaxSearchCount <= 0)
                {
                    _settings.MaxSearchCount = 30;
                }
                try
                {
                    var searchList = _api.Search(searchQuery, token, _settings.MaxSearchCount);
                    for (int i = 0; i < searchList.Count; i++)
                    {
                        if (token.IsCancellationRequested) { return results; }
                        SearchResult searchResult = searchList[i];
                        var r = CreateResult(searchQuery, searchResult, i);
                        results.Add(r);
                    }

                }
                catch (IPCErrorException)
                {
                    results.Add(new Result
                    {
                        Title = Properties.Resources.Community_plugin_everything_is_not_running,
                        IcoPath = WarningIconPath
                    });
                }
                catch (System.Exception e)
                {
                    results.Add(new Result
                    {
                        Title = Properties.Resources.Community_plugin_everything_query_error,
                        SubTitle = e.Message,
                        Action = _ =>
                        {
                            Clipboard.SetText(e.Message + "\r\n" + e.StackTrace);
                            _context.API.ShowMsg(Properties.Resources.Community_plugin_everything_copied, null, string.Empty);
                            return false;
                        },
                        IcoPath = "Images\\error.png"
                    });
                }
            }
            return results;
        }

        private Result CreateResult(string keyword, SearchResult searchResult, int index)
        {
            var path = searchResult.FullPath;

            string workingDir = null;
            if (_settings.UseLocationAsWorkingDir)
                workingDir = Path.GetDirectoryName(path);

            var toolTipTitle = string.Format(CultureInfo.CurrentCulture, "{0} : {1}", Properties.Resources.Community_plugin_everything_name, searchResult.FileName);
            var toolTipText = string.Format(CultureInfo.CurrentCulture, "{0} : {1}", Properties.Resources.Community_plugin_everything_path, searchResult.FullPath);

            var r = new Result(searchResult.FileNameHightData, searchResult.FullPathHightData)
            {
                Score = _settings.MaxSearchCount - index,
                Title = searchResult.FileName,
                SubTitle = Properties.Resources.Community_plugin_everything_subtitle_header + ": " + searchResult.FullPath,
                IcoPath = searchResult.FullPath,
                ToolTipData = new ToolTipData(toolTipTitle, toolTipText),
                ContextData = searchResult,
                Action = _ =>
                {
                    using (var process = new Process())
                    {
                        process.StartInfo.FileName = path;
                        process.StartInfo.WorkingDirectory = string.IsNullOrWhiteSpace(workingDir) ? string.Empty : workingDir;
                        process.StartInfo.Arguments = string.Empty;

                        process.StartInfo.UseShellExecute = true;

                        try
                        {
                            process.Start();
                            return true;
                        }
                        catch (Win32Exception)
                        {
                            return false;
                        }
                    }
                }
            };
            return r;
        }

        public void Save()
        {
            _storage.Save();
        }

        public void UpdateSettings(PowerLauncherPluginSettings settings)
        {
            // nothing to do
        }
    }
}
