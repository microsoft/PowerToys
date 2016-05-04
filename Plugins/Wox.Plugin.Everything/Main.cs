using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using Wox.Infrastructure.Storage;
using Wox.Plugin.Everything.Everything;

namespace Wox.Plugin.Everything
{
    public class Main : IPlugin, IPluginI18n, IContextMenu, ISavable
    {
        private readonly EverythingAPI _api = new EverythingAPI();

        public const string SDK = "EverythingSDK";
        public const string DLL = "Everything.dll";
        internal static string SDKPath;

        private PluginInitContext _context;

        private Settings _settings;
        private PluginJsonStorage<Settings> _storage;

        public void Save()
        {
            _storage.Save();
        }

        public List<Result> Query(Query query)
        {
            var results = new List<Result>();
            if (!string.IsNullOrEmpty(query.Search))
            {
                var keyword = query.Search;
                if (_settings.MaxSearchCount <= 0)
                {
                    _settings.MaxSearchCount = 50;
                }

                try
                {
                    var searchList = _api.Search(keyword, maxCount: _settings.MaxSearchCount).ToList();
                    foreach (var s in searchList)
                    {
                        var path = s.FullPath;
                        Result r = new Result();
                        r.Title = Path.GetFileName(path);
                        r.SubTitle = path;
                        r.IcoPath = path;
                        r.Action = c =>
                        {
                            bool hide;
                            try
                            {
                                Process.Start(new ProcessStartInfo
                                {
                                    FileName = path,
                                    UseShellExecute = true
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
                        };
                        r.ContextData = s;
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

            _api.Reset();

            return results;
        }

        [DllImport("kernel32.dll")]
        private static extern int LoadLibrary(string name);

        private List<ContextMenu> GetDefaultContextMenu()
        {
            List<ContextMenu> defaultContextMenus = new List<ContextMenu>();
            ContextMenu openFolderContextMenu = new ContextMenu
            {
                Name = _context.API.GetTranslation("wox_plugin_everything_open_containing_folder"),
                Command = "explorer.exe",
                Argument = " /select,\"{path}\"",
                ImagePath = "Images\\folder.png"
            };

            defaultContextMenus.Add(openFolderContextMenu);
            return defaultContextMenus;
        }

        public void Init(PluginInitContext context)
        {
            _context = context;
            _storage = new PluginJsonStorage<Settings>();
            _settings = _storage.Load();

            var pluginDirectory = context.CurrentPluginMetadata.PluginDirectory;
            var bundledSDKDirectory = Path.Combine(pluginDirectory, SDK, CpuType());
            var bundledSDKPath = Path.Combine(bundledSDKDirectory, DLL);

            var SDKDirectory = Path.Combine(_storage.DirectoryPath, SDK, CpuType());
            SDKPath = Path.Combine(SDKDirectory, DLL);
            if (!Directory.Exists(SDKDirectory))
            {
                Directory.CreateDirectory(SDKDirectory);
            }

            if (!File.Exists(SDKPath))
            {
                File.Copy(bundledSDKPath, SDKPath);
            }
            else
            {
                var newSDK = new FileInfo(bundledSDKPath).LastWriteTimeUtc;
                var oldSDK = new FileInfo(SDKPath).LastWriteTimeUtc;
                if (oldSDK != newSDK)
                {
                    File.Copy(bundledSDKPath, SDKPath, true);
                }
            }

            LoadLibrary(SDKPath);
        }

        private string CpuType()
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

        public List<Result> LoadContextMenus(Result selectedResult)
        {
            SearchResult record = selectedResult.ContextData as SearchResult;
            List<Result> contextMenus = new List<Result>();
            if (record == null) return contextMenus;

            List<ContextMenu> availableContextMenus = new List<ContextMenu>();
            availableContextMenus.AddRange(GetDefaultContextMenu());
            availableContextMenus.AddRange(_settings.ContextMenus);

            if (record.Type == ResultType.File)
            {
                foreach (ContextMenu contextMenu in availableContextMenus)
                {
                    var menu = contextMenu;
                    contextMenus.Add(new Result
                    {
                        Title = contextMenu.Name,
                        Action = _ =>
                        {
                            string argument = menu.Argument.Replace("{path}", record.FullPath);
                            try
                            {
                                Process.Start(menu.Command, argument);
                            }
                            catch
                            {
                                _context.API.ShowMsg(string.Format(_context.API.GetTranslation("wox_plugin_everything_canot_start"), record.FullPath), string.Empty, string.Empty);
                                return false;
                            }
                            return true;
                        },
                        IcoPath = contextMenu.ImagePath
                    });
                }
            }

            return contextMenus;
        }
    }
}
