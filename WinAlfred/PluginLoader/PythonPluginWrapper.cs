using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using WinAlfred.Plugin;

namespace WinAlfred.PluginLoader
{
    public class PythonPluginWrapper : IPlugin
    {
        private PluginMetadata metadata;

        [DllImport("PyWinAlfred.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public extern static void ExecPython(string directory, string file, string query);

        static PythonPluginWrapper()
        {

        }

        public PythonPluginWrapper(PluginMetadata metadata)
        {
            this.metadata = metadata;
        }


        public List<Result> Query(Query query)
        {
            List<Result> results = new List<Result>();
            ExecPython(metadata.PluginDirecotry, metadata.ExecuteFile.Replace(".py", ""), query.RawQuery);
            results.Add(new Result()
            {
            });
            return results;
        }

        public void Init()
        {

        }
    }
}
