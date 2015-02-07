using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Wox.Infrastructure;
using System.Reflection;
using Wox.Plugin.Everything.Everything;

namespace Wox.Plugin.Everything
{
    public class Main : IPlugin, IPluginI18n
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
                    ContextMenuStorage.Instance.MaxSearchCount = 100;
                    ContextMenuStorage.Instance.Save();
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
                        r.ContextMenu = GetContextMenu(s);
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

        private List<Result> GetContextMenu(SearchResult record)
        {
            List<Result> contextMenus = new List<Result>();

            List<ContextMenu> availableContextMenus = new List<ContextMenu>();
            availableContextMenus.AddRange(GetDefaultContextMenu());
            availableContextMenus.AddRange(ContextMenuStorage.Instance.ContextMenus);

            if (record.Type == ResultType.File)
            {
                foreach (ContextMenu contextMenu in availableContextMenus)
                {
                    contextMenus.Add(new Result()
                    {
                        Title = contextMenu.Name,
                        Action = _ =>
                        {
                            string argument = contextMenu.Argument.Replace("{path}", record.FullPath);
                            try
                            {
                                Process.Start(contextMenu.Command, argument);
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
            if (!CheckEverythingIsRunning())
            {
                try
                {
                    Process p = new Process();
                    p.StartInfo.Verb = "runas";
                    p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                    p.StartInfo.FileName = GetEverythingPath();
                    p.StartInfo.UseShellExecute = true;
                    p.Start();
                }
                catch (Exception e)
                {
                    context.API.ShowMsg("Start Everything failed");
                }

            }
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
    }
}
