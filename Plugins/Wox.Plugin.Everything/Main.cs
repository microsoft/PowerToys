using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Wox.Plugin.Everything
{
    public class Main : IPlugin
    {
        Wox.Plugin.PluginInitContext context;
        EverythingAPI api = new EverythingAPI();
        private static List<string> imageExts = new List<string>() { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".tiff", ".ico" };

        public List<Result> Query(Query query)
        {
            var results = new List<Result>();
            if (query.ActionParameters.Count > 0 && query.ActionParameters[0].Length > 0)
            {
                var keyword = string.Join(" ", query.ActionParameters.ToArray());
                var enumerable = api.Search(keyword, 0, 100);
                foreach (var s in enumerable)
                {
                    var path = s.FullPath;
                    Result r  = new Result();
                    r.Title = Path.GetFileName(path);
                    r.SubTitle = path;
                    r.IcoPath = GetIconPath(s);
                    r.Action = (c) =>
                    {
                        context.HideApp();
                        System.Diagnostics.ProcessStartInfo info = new System.Diagnostics.ProcessStartInfo();
                        info.UseShellExecute = true;
                        info.FileName = path;
                        try
                        {
                            System.Diagnostics.Process.Start(info);
                        }
                        catch (Exception ex)
                        {
                            context.ShowMsg("Could not start " + r.Title, ex.Message, null);
                        }
                        return true;
                    };
                    results.Add(r);
                }
            }

            api.Reset();

            return results;
        }

        private string GetIconPath(SearchResult s)
        {
            if (s.Type == ResultType.Folder)
            {
                return "Images\\folder.png";
            }
            else
            {
                var ext = Path.GetExtension(s.FullPath);
                if (!string.IsNullOrEmpty(ext) && imageExts.Contains(ext.ToLower()))
                    return "Images\\image.png";
                else
                    return s.FullPath;
            }
        }
        
        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern int LoadLibrary(string name);

        public void Init(Wox.Plugin.PluginInitContext context)
        {
            this.context = context;

            LoadLibrary(Path.Combine(
                Path.Combine(context.CurrentPluginMetadata.PluginDirecotry, (IntPtr.Size == 4) ? "x86" : "x64"),
                "Everything.dll"
            ));
            //init everything
        }
    }
}
