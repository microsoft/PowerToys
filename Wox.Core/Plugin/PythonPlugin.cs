using System;
using System.Diagnostics;
using Wox.Core.UserSettings;
using Wox.Plugin;

namespace Wox.Core.Plugin
{
    internal class PythonPlugin : JsonRPCPlugin
    {
        private readonly ProcessStartInfo _startInfo;
        public override string SupportedLanguage { get; set; } = AllowedLanguage.Python;

        public PythonPlugin(string filename)
        {
            _startInfo = new ProcessStartInfo
            {
                FileName = @"C:\Program Files\Python 3.5\pythonw.exe",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
        }

        protected override string ExecuteQuery(Query query)
        {
            JsonRPCServerRequestModel request = new JsonRPCServerRequestModel
            {
                Method = "query",
                Parameters = new object[] { query.Search },
                HttpProxy = HttpProxy.Instance
            };
            //Add -B flag to tell python don't write .py[co] files. Because .pyc contains location infos which will prevent python portable
            _startInfo.Arguments = $"-B \"{context.CurrentPluginMetadata.ExecuteFilePath}\" \"{request}\"";

            return Execute(_startInfo);
        }

        protected override string ExecuteCallback(JsonRPCRequestModel rpcRequest)
        {
            _startInfo.Arguments = $"-B \"{context.CurrentPluginMetadata.ExecuteFilePath}\" \"{rpcRequest}\"";
            return Execute(_startInfo);
        }
    }
}