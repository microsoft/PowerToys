using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Windows;
using Wox.Infrastructure.Storage;
using Wox.Infrastructure;
using Wox.Plugin.Everything.Everything;

namespace Wox.Plugin.Everything
{
    public class Main : IPlugin, IPluginI18n, IContextMenu
    {
        private readonly EverythingAPI _api = new EverythingAPI();
        private static readonly List<string> ImageExts = new List<string> { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".tiff", ".ico" };
        private static readonly List<string> ExecutableExts = new List<string> { ".exe" };

        private const string EverythingProcessName = "Everything";
        private const string PortableEverything = "PortableEverything";
        internal static string LibraryPath;

        private PluginInitContext _context;

        private readonly Settings _settings;
        private readonly PluginJsonStorage<Settings> _storage;

        public Main()
        {
            _storage = new PluginJsonStorage<Settings>();
            _settings = _storage.Load();
        }

        ~Main()
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

                if (keyword == "uninstalleverything")
                {
                    Result r = new Result();
                    r.Title = "Uninstall Everything";
                    r.SubTitle = "You need to uninstall everything service if you can not move/delete wox folder";
                    r.IcoPath = "Images\\find.png";
                    r.Action = c =>
                    {
                        UnInstallEverything();
                        return true;
                    };
                    r.Score = 2000;
                    results.Add(r);
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
                        r.IcoPath = GetIconPath(s);
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
                    StartEverything();
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

        private string GetIconPath(SearchResult s)
        {
            var ext = Path.GetExtension(s.FullPath);
            if (s.Type == ResultType.Folder)
            {
                return "Images\\folder.png";
            }
            else if (!string.IsNullOrEmpty(ext))
            {
                if (ImageExts.Contains(ext.ToLower()))
                {
                    return "Images\\image.png";
                }
                else if (ExecutableExts.Contains(ext.ToLower()))
                {
                    return s.FullPath;
                }
            }

            return "Images\\file.png";
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

            var pluginDirectory = context.CurrentPluginMetadata.PluginDirectory;
            var libraryDirectory = Path.Combine(pluginDirectory, PortableEverything, CpuType());
            LibraryPath = Path.Combine(libraryDirectory, "Everything.dll");
            LoadLibrary(LibraryPath);
            //Helper.AddDLLDirectory(libraryDirectory);

            StartEverything();
        }

        private string CpuType()
        {
            return Environment.Is64BitOperatingSystem ? "x64" : "x86";
        }


        private void StartEverything()
        {
            if (!CheckEverythingServiceRunning())
            {
                if (InstallAndRunEverythingService())
                {
                    StartEverythingClient();
                }
            }
            else
            {
                StartEverythingClient();
            }
        }

        private bool InstallAndRunEverythingService()
        {
            try
            {
                Process p = new Process();
                p.StartInfo.Verb = "runas";
                p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                p.StartInfo.FileName = GetEverythingPath();
                p.StartInfo.UseShellExecute = true;
                p.StartInfo.Arguments = "-install-service";
                p.Start();
                return true;
            }
            catch (Exception e)
            {
                return false;
            }
        }

        private bool UnInstallEverything()
        {
            try
            {
                Process p = new Process();
                p.StartInfo.Verb = "runas";
                p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                p.StartInfo.FileName = GetEverythingPath();
                p.StartInfo.UseShellExecute = true;
                p.StartInfo.Arguments = "-uninstall-service";
                p.Start();

                Process[] proc = Process.GetProcessesByName(EverythingProcessName);
                foreach (Process process in proc)
                {
                    process.Kill();
                }
                return true;
            }
            catch (Exception e)
            {
                return false;
            }
        }

        private void StartEverythingClient()
        {
            try
            {
                Process p = new Process();
                p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                p.StartInfo.FileName = GetEverythingPath();
                p.StartInfo.UseShellExecute = true;
                p.StartInfo.Arguments = "-startup";
                p.Start();
            }
            catch (Exception e)
            {
                _context.API.ShowMsg("Start Everything failed");
            }
        }

        private bool CheckEverythingServiceRunning()
        {
            try
            {
                ServiceController sc = new ServiceController(EverythingProcessName);
                return sc.Status == ServiceControllerStatus.Running;
            }
            catch
            {

            }
            return false;
        }

        private bool CheckEverythingIsRunning()
        {
            return Process.GetProcessesByName(EverythingProcessName).Length > 0;
        }

        private string GetEverythingPath()
        {
            string directory = Path.Combine(
                _context.CurrentPluginMetadata.PluginDirectory,
                PortableEverything, CpuType(),
                "Everything.exe"
                );
            return directory;
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
