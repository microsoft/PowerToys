using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Python.Runtime;
using WinAlfred.Helper;
using WinAlfred.Plugin;
using WinAlfred.PluginLoader;

namespace WinAlfred.Commands
{
    public class PluginCommand : BaseCommand
    {
        private string currentPythonModulePath = string.Empty;
        private IntPtr GIL;

        public PluginCommand(MainWindow mainWindow)
            : base(mainWindow)
        {

        }

        public override void Dispatch(Query q)
        {
            PluginPair thirdPlugin = Plugins.AllPlugins.FirstOrDefault(o => o.Metadata.ActionKeyword == q.ActionName);
            if (thirdPlugin != null && !string.IsNullOrEmpty(thirdPlugin.Metadata.ActionKeyword))
            {
                if (thirdPlugin.Metadata.Language == AllowedLanguage.Python)
                {
                    SwitchPythonEnv(thirdPlugin);
                }
                ThreadPool.QueueUserWorkItem(t =>
                {
                    try
                    {
                        List<Result> r = thirdPlugin.Plugin.Query(q);
                        r.ForEach(o =>
                        {
                            o.PluginDirectory = thirdPlugin.Metadata.PluginDirecotry;
                            o.OriginQuery = q;
                        });
                        UpdateResultView(r);
                    }
                    catch (Exception queryException)
                    {
                        Log.Error(string.Format("Plugin {0} query failed: {1}", thirdPlugin.Metadata.Name,
                            queryException.Message));
#if (DEBUG)
                        {
                            throw;
                        }
#endif
                    }
                });
            }
        }

        private void SwitchPythonEnv(PluginPair thirdPlugin)
        {
            if (currentPythonModulePath != thirdPlugin.Metadata.PluginDirecotry)
            {
                //this must initial in main thread
                currentPythonModulePath = thirdPlugin.Metadata.PluginDirecotry;

                if (GIL != IntPtr.Zero)
                {
                    Runtime.PyEval_RestoreThread(GIL);
                    PythonEngine.Shutdown();
                }
                PythonEngine.Initialize();
                IntPtr pyStrPtr = Runtime.PyString_FromString(thirdPlugin.Metadata.PluginDirecotry);
                IntPtr SysDotPath = Runtime.PySys_GetObject("path");
                Runtime.PyList_Append(SysDotPath, pyStrPtr);
                GIL = PythonEngine.BeginAllowThreads();
            }
        }
    }
}
