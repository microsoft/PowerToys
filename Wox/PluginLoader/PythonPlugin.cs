using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Wox.Helper;
using Wox.JsonRPC;
using Wox.Plugin;

namespace Wox.PluginLoader
{
    public class PythonPlugin : BasePlugin
    {
        private static string woxDirectory = Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath);
        private ProcessStartInfo startInfo;

        public override string SupportedLanguage
        {
            get { return AllowedLanguage.Python; }
        }

        public PythonPlugin()
        {
            startInfo = new ProcessStartInfo
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
            string additionalPythonPath = string.Format("{0};{1}",
                                                        Path.Combine(woxDirectory, "PythonHome\\DLLs"),
                                                        Path.Combine(woxDirectory, "PythonHome\\Lib\\site-packages"));
            if (!startInfo.EnvironmentVariables.ContainsKey("PYTHONPATH"))
            {

                startInfo.EnvironmentVariables.Add("PYTHONPATH", additionalPythonPath);
            }
            else
            {
                startInfo.EnvironmentVariables["PYTHONPATH"] = additionalPythonPath;
            }
        }

        protected override string ExecuteQuery(Query query)
        {
            JsonRPCServerRequestModel request = new JsonRPCServerRequestModel()
                {
                    Method = "query",
                    Parameters = new object[] { query.GetAllRemainingParameter() },
                    HttpProxy = HttpProxy.Instance
                };
            //Add -B flag to tell python don't write .py[co] files. Because .pyc contains location infos which will prevent python portable
            startInfo.FileName = Path.Combine(woxDirectory, "PythonHome\\pythonw.exe");
            startInfo.Arguments = string.Format("-B \"{0}\" \"{1}\"", context.CurrentPluginMetadata.ExecuteFilePath, request);

            return Execute(startInfo);
        }

        protected override string ExecuteAction(JsonRPCRequestModel rpcRequest)
        {
            startInfo.FileName = Path.Combine(woxDirectory, "PythonHome\\pythonw.exe");
            startInfo.Arguments = string.Format("-B \"{0}\" \"{1}\"", context.CurrentPluginMetadata.ExecuteFilePath, rpcRequest);
            return Execute(startInfo);
        }
    }
}