using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Python.Runtime;
using Wox.Plugin;
using Wox.Helper;
using Wox.RPC;

namespace Wox.PluginLoader
{
    public class PythonPluginWrapper : BasePluginWrapper
    {
        private static string woxDirectory = Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath);

        public override List<string> GetAllowedLanguages()
        {
            return  new List<string>()
            {
                AllowedLanguage.Python
            };
        }

        protected override string GetFileName()
        {
            return Path.Combine(woxDirectory, "PYTHONTHOME\\Scripts\\python.exe");
        }

        protected override string GetQueryArguments(Query query)
        {
            return string.Format("{0} \"{1}\"",
                     context.CurrentPluginMetadata.ExecuteFilePath,
                     JsonRPC.GetRPC("query", query.GetAllRemainingParameter()));
        }

        protected override string GetActionJsonRPCArguments(ActionJsonRPCResult result)
        {
            return string.Format("{0} \"{1}\"", context.CurrentPluginMetadata.ExecuteFilePath,
                                  result.ActionJSONRPC);
        }
    }
}
