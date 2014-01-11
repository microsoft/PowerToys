using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Documents;
using Newtonsoft.Json;
using Python.Runtime;
using WinAlfred.Plugin;

namespace WinAlfred.PluginLoader
{
    public class PythonPluginWrapper : IPlugin
    {

        private PluginMetadata metadata;

        public PythonPluginWrapper(PluginMetadata metadata)
        {
            this.metadata = metadata;
        }

        public List<Result> Query(Query query)
        {
            try
            {
                string jsonResult = InvokeFunc(metadata.PluginDirecotry, metadata.ExecuteFileName.Replace(".py", ""), "query", query.RawQuery);
                if (string.IsNullOrEmpty(jsonResult))
                {
                    return new List<Result>();
                }

                List<PythonResult> o = JsonConvert.DeserializeObject<List<PythonResult>>(jsonResult);
                List<Result> r = new List<Result>();
                foreach (PythonResult pythonResult in o)
                {
                    PythonResult ps = pythonResult;
                    if (!string.IsNullOrEmpty(ps.ActionName))
                    {
                        ps.Action = () => InvokeFunc(metadata.PluginDirecotry, metadata.ExecuteFileName.Replace(".py", ""), ps.ActionName, ps.ActionPara);
                    }
                    r.Add(ps);
                }
                return r;
            }
            catch (Exception)
            {
#if (DEBUG)
                {
                    throw;
                }
#endif
            }

        }

        private string InvokeFunc(string path, string moduleName, string func, string para)
        {
            IntPtr gs = PythonEngine.AcquireLock();

            PyObject module = PythonEngine.ImportModule(moduleName);
            PyObject res = module.InvokeMethod(func, new PyString(para));
            string json = Runtime.GetManagedString(res.Handle);

            PythonEngine.ReleaseLock(gs);

            return json;
        }

        public void Init(PluginInitContext context)
        {

        }
    }
}
