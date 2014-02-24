using System;
using System.Collections.Generic;
using System.Linq;
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
                        ps.Action = (context) => InvokeFunc(ps.ActionName, GetPythonActionContext(context), new PyString(ps.ActionPara));
                    }
                    r.Add(ps);
                }
                return r;
            }
            catch (Exception e)
            {
#if (DEBUG)
                {
                    throw new WoxPythonException(e.Message);
                }
#endif
                Log.Error(string.Format("Python Plugin {0} query failed: {1}", metadata.Name, e.Message));
            }

            return new List<Result>();
        }

        private PyObject GetPythonActionContext(ActionContext context)
        {
            PyDict dict = new PyDict();
            PyDict specialKeyStateDict = new PyDict();
            specialKeyStateDict["CtrlPressed"] = new PyString(context.SpecialKeyState.CtrlPressed.ToString());
            specialKeyStateDict["AltPressed"] = new PyString(context.SpecialKeyState.AltPressed.ToString());
            specialKeyStateDict["WinPressed"] = new PyString(context.SpecialKeyState.WinPressed.ToString());
            specialKeyStateDict["ShiftPressed"] = new PyString(context.SpecialKeyState.ShiftPressed.ToString());

            dict["SpecialKeyState"] = specialKeyStateDict;
            return dict;
        }

        private string InvokeFunc(string func, params PyObject[] paras)
        {
            string json = null;

            IntPtr gs = PythonEngine.AcquireLock();

            PyObject module = PythonEngine.ImportModule(moduleName);
            if (module == null)
            {
                string error = string.Format("Python Invoke failed: {0} doesn't has module {1}",
                   metadata.ExecuteFilePath, moduleName);
                Log.Error(error);
                return json;
            }

            if (module.HasAttr(func))
            {
                try
                {
                    PyObject res = paras.Length > 0 ? module.InvokeMethod(func, paras) : module.InvokeMethod(func);
                    json = Runtime.GetManagedString(res.Handle);
                }
                catch (Exception e)
                {
                    string error = string.Format("Python Invoke failed: {0}", e.Message);
                    Log.Error(error);
#if (DEBUG)
                    {
                        throw new WoxPythonException(error);
                    }
#endif
                }

            }
            else
            {
                string error = string.Format("Python Invoke failed: {0} doesn't has function {1}",
                    metadata.ExecuteFilePath, func);
                Log.Error(error);
#if (DEBUG)
                {
                    throw new WoxPythonException(error);
                }
#endif
            }

            PythonEngine.ReleaseLock(gs);

            return json;
        }

        private string InvokeFunc(string func, params string[] para)
        {
            PyObject[] paras = { };
            if (para != null && para.Length > 0)
            {
                paras = para.Select(o => new PyString(o)).ToArray();
            }
            return InvokeFunc(func, paras);
        }

        public void Init(PluginInitContext context)
        {

        }
    }
}
