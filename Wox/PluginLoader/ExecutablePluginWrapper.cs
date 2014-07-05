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
    public class ExecutablePluginWrapper : IPlugin
    {
        private PluginInitContext context;
        private static string executeDirectory = Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath);

        public List<Result> Query(Query query)
        {
            List<Result> results = new List<Result>();
            try
            {
                ProcessStartInfo start = new ProcessStartInfo();
                start.FileName = Path.Combine(executeDirectory, "PYTHONTHOME\\Scripts\\python.exe");
                start.Arguments = string.Format("{0} \"{1}\"",
                    context.CurrentPluginMetadata.ExecuteFilePath,
                    RPC.JsonRPC.GetRPC("query", query.GetAllRemainingParameter()));
                start.UseShellExecute = false;
                start.CreateNoWindow = true;
                start.RedirectStandardOutput = true;
                using (Process process = Process.Start(start))
                {
                    if (process != null)
                    {
                        using (StreamReader reader = process.StandardOutput)
                        {
                            string output = reader.ReadToEnd();
                            if (!string.IsNullOrEmpty(output))
                            {
                                JsonPRCModel rpc = JsonConvert.DeserializeObject<JsonPRCModel>(output);
                                var rpcresults = JsonConvert.DeserializeObject<List<ActionJsonRPCResult>>(rpc.result);
                                List<Result> r = new List<Result>();
                                foreach (ActionJsonRPCResult result in rpcresults)
                                {
                                    if (!string.IsNullOrEmpty(result.ActionJSONRPC))
                                    {
                                        result.Action = (context) =>
                                        {
                                            return true;
                                        };
                                    }
                                    r.Add(result);
                                }
                                return r;
                            }
                        }
                    }
                }
            }
            catch
            {
            }

            return results;
        }

        public void Init(PluginInitContext context)
        {
            this.context = context;
        }
    }
}
