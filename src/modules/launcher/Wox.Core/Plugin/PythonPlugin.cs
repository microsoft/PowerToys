using System;
using System.Diagnostics;
using System.IO;
using Wox.Infrastructure;
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
                FileName = filename,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            // temp fix for issue #667
            var path = Path.Combine(Constant.ProgramDirectory, JsonRPC);
            _startInfo.EnvironmentVariables["PYTHONPATH"] = path;

        }

        protected override string ExecuteQuery(Query query)
        {
            JsonRPCServerRequestModel request = new JsonRPCServerRequestModel
            {
                Method = "query",
                Parameters = new object[] { query.Search },
            };
            //Add -B flag to tell python don't write .py[co] files. Because .pyc contains location infos which will prevent python portable
            _startInfo.Arguments = $"-B \"{context.CurrentPluginMetadata.ExecuteFilePath}\" \"{request}\"";
            // todo happlebao why context can't be used in constructor
            _startInfo.WorkingDirectory = context.CurrentPluginMetadata.PluginDirectory;

            return Execute(_startInfo);
        }

        protected override string ExecuteCallback(JsonRPCRequestModel rpcRequest)
        {
            _startInfo.Arguments = $"-B \"{context.CurrentPluginMetadata.ExecuteFilePath}\" \"{rpcRequest}\"";
            _startInfo.WorkingDirectory = context.CurrentPluginMetadata.PluginDirectory;
            return Execute(_startInfo);
        }

        protected override string ExecuteContextMenu(Result selectedResult) {
            JsonRPCServerRequestModel request = new JsonRPCServerRequestModel {
                Method = "context_menu",
                Parameters = new object[] { selectedResult.ContextData },
            };
            _startInfo.Arguments = $"-B \"{context.CurrentPluginMetadata.ExecuteFilePath}\" \"{request}\"";
            _startInfo.WorkingDirectory = context.CurrentPluginMetadata.PluginDirectory;

            return Execute(_startInfo);
        }
    }
}