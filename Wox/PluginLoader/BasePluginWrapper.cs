using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Wox.Plugin;
using Wox.RPC;

namespace Wox.PluginLoader
{
    public abstract class BasePluginWrapper : IPlugin
    {
        protected PluginInitContext context;

        public abstract List<string> GetAllowedLanguages();
        protected abstract string GetFileName();

        protected abstract string GetQueryArguments(Query query);

        protected abstract string GetActionJsonRPCArguments(ActionJsonRPCResult result);

        public List<Result> Query(Query query)
        {
            string fileName = GetFileName();
            string arguments = GetQueryArguments(query);
            string output = Execute(fileName, arguments);
            if (!string.IsNullOrEmpty(output))
            {
                try
                {
                    JsonPRCModel rpc = JsonConvert.DeserializeObject<JsonPRCModel>(output);
                    List<ActionJsonRPCResult> rpcresults =
                        JsonConvert.DeserializeObject<List<ActionJsonRPCResult>>(rpc.result);
                    List<Result> results = new List<Result>();
                    foreach (ActionJsonRPCResult result in rpcresults)
                    {
                        if (!string.IsNullOrEmpty(result.ActionJSONRPC))
                        {
                            ActionJsonRPCResult resultCopy = result;
                            result.Action = (c) =>
                            {
                                Execute(fileName, GetActionJsonRPCArguments(resultCopy));
                                return true;
                            };
                        }
                        results.Add(result);
                    }
                    return results;
                }
                catch
                {
                }
            }
            return null;
        }

        private string Execute(string fileName, string arguments)
        {
            try
            {
                ProcessStartInfo start = new ProcessStartInfo();
                start.FileName = fileName;
                start.Arguments = arguments;
                start.UseShellExecute = false;
                start.CreateNoWindow = true;
                start.RedirectStandardOutput = true;
                using (Process process = Process.Start(start))
                {
                    if (process != null)
                    {
                        using (StreamReader reader = process.StandardOutput)
                        {
                            return reader.ReadToEnd();
                        }
                    }
                }
            }
            catch
            {
                return null;
            }
            return null;
        }

        public void Init(PluginInitContext ctx)
        {
            this.context = ctx;
        }
    }
}
