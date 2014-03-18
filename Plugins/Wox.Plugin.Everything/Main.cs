using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Wox.Plugin.Everything
{
    public class Main : IPlugin
    {
        Wox.Plugin.PluginInitContext context;
        EverythingAPI api = new EverythingAPI();

        public List<Result> Query(Query query)
        {
            var results = new List<Result>();
            if (query.ActionParameters.Count > 0 && query.ActionParameters[0].Length > 0)
            {
                IEnumerable<string> enumerable = api.Search(query.ActionParameters[0], 0, 100);
                foreach (string s in enumerable)
                {
                    var path = s;
                    Result r  = new Result();
                    r.Title = Path.GetFileName(path);
                    r.SubTitle = path;
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
