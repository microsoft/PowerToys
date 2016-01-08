using System.Diagnostics;
using System.IO;
using Wox.Core.UserSettings;
using Wox.Infrastructure;
using Wox.Plugin;

namespace Wox.Core.Plugin
{
    internal class PythonPlugin : JsonRPCPlugin
    {
        private static readonly string PythonHome = Path.Combine(WoxDirectroy.Executable, "PythonHome");
        private readonly ProcessStartInfo _startInfo;

        public override string SupportedLanguage => AllowedLanguage.Python;

        public PythonPlugin()
        {
            _startInfo = new ProcessStartInfo
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            string additionalPythonPath = $"{Path.Combine(PythonHome, "DLLs")};{Path.Combine(PythonHome, "Lib", "site-packages")}";
            if (!_startInfo.EnvironmentVariables.ContainsKey("PYTHONPATH"))
            {

                _startInfo.EnvironmentVariables.Add("PYTHONPATH", additionalPythonPath);
            }
            else
            {
                _startInfo.EnvironmentVariables["PYTHONPATH"] = additionalPythonPath;
            }
        }

        protected override string ExecuteQuery(Query query)
        {
            JsonRPCServerRequestModel request = new JsonRPCServerRequestModel
            {
                Method = "query",
                Parameters = new object[] { query.GetAllRemainingParameter() },
                HttpProxy = HttpProxy.Instance
            };
            //Add -B flag to tell python don't write .py[co] files. Because .pyc contains location infos which will prevent python portable
            _startInfo.FileName = Path.Combine(PythonHome, "pythonw.exe");
            _startInfo.Arguments = $"-B \"{context.CurrentPluginMetadata.ExecuteFilePath}\" \"{request}\"";

            return Execute(_startInfo);
        }

        protected override string ExecuteCallback(JsonRPCRequestModel rpcRequest)
        {
            _startInfo.FileName = Path.Combine(PythonHome, "pythonw.exe");
            _startInfo.Arguments = $"-B \"{context.CurrentPluginMetadata.ExecuteFilePath}\" \"{rpcRequest}\"";
            return Execute(_startInfo);
        }
    }
}