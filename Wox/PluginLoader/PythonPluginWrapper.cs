using System.Collections.Generic;
using System.IO;
using Wox.Plugin;
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

        protected override string ExecuteQuery(Query query)
        {
            string fileName = Path.Combine(woxDirectory, "PYTHONTHOME\\Scripts\\python.exe");
            string parameters = string.Format("{0} \"{1}\"", context.CurrentPluginMetadata.ExecuteFilePath,
                     JsonRPC.Send("query", query.GetAllRemainingParameter()));
            return Execute(fileName, parameters);
        }

        protected override string ExecuteAction(string rpcRequest)
        {
            string fileName = Path.Combine(woxDirectory, "PYTHONTHOME\\Scripts\\python.exe");
            string parameters = string.Format("{0} \"{1}\"", context.CurrentPluginMetadata.ExecuteFilePath,
                                              rpcRequest);
            return Execute(fileName, parameters);
        }
    }
}