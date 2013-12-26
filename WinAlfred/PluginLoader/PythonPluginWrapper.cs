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
        private extern static IntPtr ExecPython(string directory, string file,string method,string para);
        [DllImport("PyWinAlfred.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private extern static void InitPythonEnv();

        public PythonPluginWrapper(PluginMetadata metadata)
        {
            this.metadata = metadata;
        }

        public List<Result> Query(Query query)
        {
            string s = Marshal.PtrToStringAnsi(ExecPython(metadata.PluginDirecotry, metadata.ExecuteFileName.Replace(".py", ""),"query",query.RawQuery));
            List<PythonResult> o = JsonConvert.DeserializeObject<List<PythonResult>>(s);
            List<Result> r = new List<Result>();
            foreach (PythonResult pythonResult in o)
            {
               PythonResult ps = pythonResult;
                ps.Action = () => ExecPython(metadata.PluginDirecotry, metadata.ExecuteFileName.Replace(".py", ""),ps.ActionName,ps.ActionPara);
                r.Add(ps);
            }
            return r;
        }

        public void Init()
        {
            InitPythonEnv();
        }
    }
}
