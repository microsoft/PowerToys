using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.ServiceProcess;
using Wox.Infrastructure;
using Wox.Plugin.Everything.Everything;

namespace Wox.Plugin.Everything
{
    public class Main : IPlugin, IPluginI18n, IContextMenu
    {
        PluginInitContext context;
        EverythingAPI api = new EverythingAPI();
        private static List<string> imageExts = new List<string>() { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".tiff", ".ico" };
        private static List<string> executableExts = new List<string>() { ".exe" };

        public List<Result> Query(Query query)
        {
            var results = new List<Result>();
            if (!string.IsNullOrEmpty(query.Search))
            {
                var keyword = query.Search;
                if (ContextMenuStorage.Instance.MaxSearchCount <= 0)
                {
                    ContextMenuStorage.Instance.MaxSearchCount = 50;
                    ContextMenuStorage.Instance.Save();
                }

                if (keyword == "uninstalleverything")
                {
                    Result r = new Result();
                    r.Title = "Uninstall Everything";
                    r.SubTitle = "You need to uninstall everything service if you can not move/delete wox folder";
                    r.IcoPath = "Images\\find.png";
                    r.Action = (c) =>
                    {
                        UnInstallEverything();
                        return true;
                    };
                    r.Score = 2000;
                    results.Add(r);
                }

                try
                {
                    var searchList = api.Search(keyword, maxCount: ContextMenuStorage.Instance.MaxSearchCount).ToList();
                    var fuzzyMather = FuzzyMatcher.Create(keyword);
                    searchList.Sort(
                        (x, y) =>
                            fuzzyMather.Evaluate(Path.GetFileName(y.FullPath)).Score -
                            fuzzyMather.Evaluate(Path.GetFileName(x.FullPath)).Score);

                    foreach (var s in searchList)
                    {
                        var path = s.FullPath;
                        Result r = new Result();
                        r.Title = Path.GetFileName(path);
                        r.SubTitle = path;
                        r.IcoPath = GetIconPath(s);
                        r.Action = (c) =>
                        {
                            context.API.HideApp();
                            context.API.ShellRun(path);
                            return true;
                        };
                        r.ContextData = s;
                        results.Add(r);
                    }
                }
                catch (IPCErrorException)
                {
                    StartEverything();
                    results.Add(new Result()
                    {
                        Title = context.API.GetTranslation("wox_plugin_everything_is_not_running"),
                        IcoPath = "Images\\warning.png"
                    });
                }
                catch (Exception e)
                {
                    results.Add(new Result()
                    {
                        Title = context.API.GetTranslation("wox_plugin_everything_query_error"),
                        SubTitle = e.Message,
                        Action = _ =>
                        {
                            System.Windows.Clipboard.SetText(e.Message + "\r\n" + e.StackTrace);
                            context.API.ShowMsg(context.API.GetTranslation("wox_plugin_everything_copied"), null, string.Empty);
                            return false;
                        },
                        IcoPath = "Images\\error.png"
                    });
                }
            }

            api.Reset();

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
                if (imageExts.Contains(ext.ToLower()))
                {
                    return "Images\\image.png";
                }
                else if (executableExts.Contains(ext.ToLower()))
                {
                    return s.FullPath;
                }
            }

            return "Images\\file.png";
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern int LoadLibrary(string name);

        private List<ContextMenu> GetDefaultContextMenu()
        {
            List<ContextMenu> defaultContextMenus = new List<ContextMenu>();
            ContextMenu openFolderContextMenu = new ContextMenu()
                   {
                       Name = context.API.GetTranslation("wox_plugin_everything_open_containing_folder"),
                       Command = "explorer.exe",
                       Argument = " /select,\"{path}\"",
                       ImagePath = "Images\\folder.png"
                   };

            defaultContextMenus.Add(openFolderContextMenu);
            return defaultContextMenus;
        }

        public void Init(PluginInitContext context)
        {
            this.context = context;
            ContextMenuStorage.Instance.API = context.API;

            LoadLibrary(Path.Combine(
                Path.Combine(context.CurrentPluginMetadata.PluginDirectory, (IntPtr.Size == 4) ? "x86" : "x64"),
                "Everything.dll"
            ));

            StartEverything();
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

                Process[] proc = Process.GetProcessesByName("Everything");
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
                context.API.ShowMsg("Start Everything failed");
            }
        }

        private bool CheckEverythingServiceRunning()
        {
            try
            {
                ServiceController sc = new ServiceController("Everything");
                return sc.Status == ServiceControllerStatus.Running;
            }
            catch
            {

            }
            return false;
        }

        private bool CheckEverythingIsRunning()
        {
            return Process.GetProcessesByName("Everything").Length > 0;
        }

        private string GetEverythingPath()
        {
            string everythingFolder = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "PortableEverything");
            return Path.Combine(everythingFolder, "Everything.exe");
        }

        public string GetLanguagesFolder()
        {
            return Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Languages");
        }

        public string GetTranslatedPluginTitle()
        {
            return context.API.GetTranslation("wox_plugin_everything_plugin_name");
        }

        public string GetTranslatedPluginDescription()
        {
            return context.API.GetTranslation("wox_plugin_everything_plugin_description");
        }

        public List<Result> LoadContextMenus(Result selectedResult)
        {
            SearchResult record = selectedResult.ContextData as SearchResult;
            List<Result> contextMenus = new List<Result>();
            if (record == null) return contextMenus;

            List<ContextMenu> availableContextMenus = new List<ContextMenu>();
            availableContextMenus.AddRange(GetDefaultContextMenu());
            availableContextMenus.AddRange(ContextMenuStorage.Instance.ContextMenus);

            if (record.Type == ResultType.File)
            {
                foreach (ContextMenu contextMenu in availableContextMenus)
                {
                    var menu = contextMenu;
                    contextMenus.Add(new Result()
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
                                context.API.ShowMsg(string.Format(context.API.GetTranslation("wox_plugin_everything_canot_start"), record.FullPath), string.Empty, string.Empty);
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
