using Microsoft.Plugin.Everything;
using Microsoft.PowerToys.Settings.UI.Lib;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using Wox.Infrastructure;
using Wox.Infrastructure.Logger;
using Wox.Infrastructure.Storage;
using Wox.Plugin.Everything.Everything;

namespace Wox.Plugin.Everything
{
    public class Main : IPlugin, ISettingProvider, IPluginI18n, IContextMenu, ISavable
    {
        public const string DLL = "Everything.dll";
        private readonly IEverythingApi _api = new EverythingApi();

        private PluginInitContext _context;
        private IContextMenu _contextMenuLoader;

        private Settings _settings;
        private PluginJsonStorage<Settings> _storage;
        private CancellationTokenSource _cancellationTokenSource;

        public void Save()
        {
            _storage.Save();
        }

        public List<Result> Query(Query query)
        {
            _cancellationTokenSource?.Cancel(); // cancel if already exist
            var cts = _cancellationTokenSource = new CancellationTokenSource();
            var results = new List<Result>();
            if (!string.IsNullOrEmpty(query.Search))
            {
                var keyword = query.Search;
                
                try
                {
                    var searchList = _api.Search(keyword, cts.Token, maxCount: _settings.MaxSearchCount);
                    if (searchList == null)
                    {
                        return results;
                    }

                    foreach (var searchResult in searchList)
                    {
                        var r = CreateResult(keyword, searchResult);
                        results.Add(r);
                    }
                }
                catch (IPCErrorException)
                {
                    results.Add(new Result
                    {
                        Title = _context.API.GetTranslation("wox_plugin_everything_is_not_running"),
                        IcoPath = "Images\\warning.png"
                    });
                }
                catch (Exception e)
                {
                    Log.Exception("EverythingPlugin", "Query Error", e);
                    results.Add(new Result
                    {
                        Title = _context.API.GetTranslation("wox_plugin_everything_query_error"),
                        SubTitle = e.Message,
                        Action = _ =>
                        {
                            Clipboard.SetText(e.Message + "\r\n" + e.StackTrace);
                            _context.API.ShowMsg(_context.API.GetTranslation("wox_plugin_everything_copied"), null, string.Empty);
                            return false;
                        },
                        IcoPath = "Images\\error.png"
                    });
                }
            }

            return results;
        }

        private Result CreateResult(string keyword, SearchResult searchResult)
        {
            var path = searchResult.FullPath;

            string workingDir = null;
            if (_settings.UseLocationAsWorkingDir)
                workingDir = Path.GetDirectoryName(path);

            var r = new Result
            {
                Title = Path.GetFileName(path),
                SubTitle = path,
                IcoPath = path,
                TitleHighlightData = StringMatcher.FuzzySearch(keyword, Path.GetFileName(path)).MatchData,
                Action = c =>
                {
                    bool hide;
                    try
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = path, UseShellExecute = true, WorkingDirectory = workingDir
                        });
                        hide = true;
                    }
                    catch (Win32Exception)
                    {
                        var name = $"Plugin: {_context.CurrentPluginMetadata.Name}";
                        var message = "Can't open this file";
                        _context.API.ShowMsg(name, message, string.Empty);
                        hide = false;
                    }

                    return hide;
                },
                ContextData = searchResult,
                SubTitleHighlightData = StringMatcher.FuzzySearch(keyword, path).MatchData
            };
            return r;
        }

        public void Init(PluginInitContext context)
        {
            _context = context;
            _contextMenuLoader = new ContextMenuLoader(context);
            _storage = new PluginJsonStorage<Settings>();
            _settings = _storage.Load();
            if (_settings.MaxSearchCount <= 0)
            {
                _settings.MaxSearchCount = Settings.DefaultMaxSearchCount;
            }

            var pluginDirectory = context.CurrentPluginMetadata.PluginDirectory;
            const string sdk = "EverythingSDK";
            var bundledSDKDirectory = Path.Combine(pluginDirectory, sdk, CpuType());
            var sdkDirectory = Path.Combine(_storage.DirectoryPath, sdk, CpuType());
            Helper.ValidateDataDirectory(bundledSDKDirectory, sdkDirectory);

            var sdkPath = Path.Combine(sdkDirectory, DLL);
            _api.Load(sdkPath);
        }

        private static string CpuType()
        {
            return Environment.Is64BitOperatingSystem ? "x64" : "x86";
        }

        public string GetTranslatedPluginTitle()
        {
            return _context.API.GetTranslation("wox_plugin_everything_plugin_name");
        }

        public string GetTranslatedPluginDescription()
        {
            return _context.API.GetTranslation("wox_plugin_everything_plugin_description");
        }

        public Control CreateSettingPanel()
        {
            return new EverythingSettings(_settings);
        }
        public void UpdateSettings(PowerLauncherSettings settings)
        {

        }

        public List<ContextMenuResult> LoadContextMenus(Result selectedResult)
        {
            return _contextMenuLoader.LoadContextMenus(selectedResult);
        }
    }
}
