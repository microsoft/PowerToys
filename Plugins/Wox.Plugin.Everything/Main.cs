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
    public class Main : IPlugin
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
                        Title = "Everything is not running, we already run it for you now. Try search again",
                        IcoPath = "Images\\warning.png"
                    });
                }
                catch (Exception e)
                {
                    results.Add(new Result()
                    {
                        Title = "Everything plugin has an error (enter to copy error message)",
                        SubTitle = e.Message,
                        Action = _ =>
                        {
                            System.Windows.Clipboard.SetText(e.Message + "\r\n" + e.StackTrace);
                            context.API.ShowMsg("Copied", "Error message has copied to your clipboard", string.Empty);
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

            if (record.Type == ResultType.File)
            {
                foreach (ContextMenu contextMenu in ContextMenuStorage.Instance.ContextMenus)
                {
                    contextMenus.Add(new Result()
                    {
                        Title = contextMenu.Name,
                        Action = _ =>
                        {
                            string argument = contextMenu.Argument.Replace("{path}", record.FullPath);
                            try
                            {
                                System.Diagnostics.Process.Start(contextMenu.Command, argument);
                            }
                            catch
                            {
                                context.API.ShowMsg("Can't start " + record.FullPath, string.Empty, string.Empty);
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

        public void Init(PluginInitContext context)
        {
            this.context = context;

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
                Process p = new Process();
                p.StartInfo.Verb = "runas";
                p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                p.StartInfo.FileName = GetEverythingPath();
                p.StartInfo.UseShellExecute = true;
                p.Start();
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
    }
}
