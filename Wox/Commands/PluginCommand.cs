using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Python.Runtime;
using Wox.Helper;
using Wox.Infrastructure.Storage.UserSettings;
using Wox.Plugin;
using Wox.PluginLoader;

namespace Wox.Commands
{
    public class PluginCommand : BaseCommand
    {
        private string currentPythonModulePath = string.Empty;
        private IntPtr GIL;

        public override void Dispatch(Query q)
        {
            PluginPair thirdPlugin = Plugins.AllPlugins.FirstOrDefault(o => o.Metadata.ActionKeyword == q.ActionName);
            if (thirdPlugin != null && !string.IsNullOrEmpty(thirdPlugin.Metadata.ActionKeyword))
            {
                var customizedPluginConfig = UserSettingStorage.Instance.CustomizedPluginConfigs.FirstOrDefault(o => o.ID == thirdPlugin.Metadata.ID);
                if (customizedPluginConfig != null && customizedPluginConfig.Disabled)
                {
                    UpdateResultView(null);
                    return;
                }

                if (thirdPlugin.Metadata.Language == AllowedLanguage.Python)
                {
                    SwitchPythonEnv(thirdPlugin);
                }
                ThreadPool.QueueUserWorkItem(t =>
                {
                    try
                    {
                        thirdPlugin.InitContext.PushResults = (qu, r) =>
                        {
                            if (r != null)
                            {
                                r.ForEach(o =>
                                {
                                    o.PluginDirectory = thirdPlugin.Metadata.PluginDirecotry;
                                    o.OriginQuery = qu;
                                });
                                UpdateResultView(r);
                            }
                        };
                        List<Result> results = thirdPlugin.Plugin.Query(q) ?? new List<Result>();
                        thirdPlugin.InitContext.PushResults(q, results);
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
                currentPythonModulePath = thirdPlugin.Metadata.PluginDirecotry;

                if (GIL != IntPtr.Zero)
                {
                    Runtime.PyEval_RestoreThread(GIL);
                    PythonEngine.Shutdown();
                }
                PythonEngine.Initialize();
                IntPtr pyStrPtr = Runtime.PyString_FromString(thirdPlugin.Metadata.PluginDirecotry);
                IntPtr sysDotPath = Runtime.PySys_GetObject("path");
                Runtime.PyList_Append(sysDotPath, pyStrPtr);
                GIL = PythonEngine.BeginAllowThreads();
            }
        }
    }
}
