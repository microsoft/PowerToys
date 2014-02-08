using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Documents;
using Newtonsoft.Json;
using Python.Runtime;
using Wox.Helper;
using Wox.Plugin;

namespace Wox.PluginLoader
{
    public class PythonPluginWrapper : IPlugin
    {

        private PluginMetadata metadata;
        private string moduleName;

        public PythonPluginWrapper(PluginMetadata metadata)
        {
            this.metadata = metadata;
            moduleName = metadata.ExecuteFileName.Replace(".py", "");
        }

        public List<Result> Query(Query query)
        {
            try
            {
                string jsonResult = InvokeFunc("query", query.RawQuery);
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
                        ps.Action = () => InvokeFunc(ps.ActionName, ps.ActionPara);
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

            return new List<Result>();
        }

        private string InvokeFunc(string func, params string[] para)
        {
            string json = "";

            PyObject[] paras = { };
            if (para != null && para.Length > 0)
            {
                paras = para.Select(o => new PyString(o)).ToArray();
            }

            IntPtr gs = PythonEngine.AcquireLock();

            PyObject module = PythonEngine.ImportModule(moduleName);
            if (module.HasAttr(func))
            {
                PyObject res = paras.Length > 0 ? module.InvokeMethod(func, paras) : module.InvokeMethod(func);
                json = Runtime.GetManagedString(res.Handle);
            }
            else
            {
                string error = string.Format("Python Invoke failed: {0} doesn't has function {1}",
                    metadata.ExecuteFilePath, func);
                Log.Error(error);
#if (DEBUG)
                {
                    throw new ArgumentException(error);
                }
#endif
            }

            PythonEngine.ReleaseLock(gs);

            return json;
        }

        public void Init(PluginInitContext context)
        {

        }
    }
}
