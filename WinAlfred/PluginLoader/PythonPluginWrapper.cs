using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Newtonsoft.Json;
using WinAlfred.Plugin;

namespace WinAlfred.PluginLoader
{
    public class PythonPluginWrapper : IPlugin
    {
        private PluginMetadata metadata;

        [DllImport("PyWinAlfred.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private extern static IntPtr ExecPython(string directory, string file, string query);

        public PythonPluginWrapper(PluginMetadata metadata)
        {
            this.metadata = metadata;
        }

        public List<Result> Query(Query query)
        {
            string s = Marshal.PtrToStringAnsi(ExecPython(metadata.PluginDirecotry, metadata.ExecuteFileName.Replace(".py", ""), query.RawQuery));
            List<Result> o = JsonConvert.DeserializeObject<List<Result>>(s);
            return o;
        }

        public void Init()
        {

        }
    }
}
