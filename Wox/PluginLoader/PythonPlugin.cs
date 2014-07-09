using System.Collections.Generic;
using System.IO;
using Wox.JsonRPC;
using Wox.Plugin;

namespace Wox.PluginLoader
{
    public class PythonPlugin : BasePlugin
    {
        private static string woxDirectory = Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath);

        public override string SupportedLanguage
        {
            get { return AllowedLanguage.Python; }
        }

        protected override string ExecuteQuery(Query query)
        {
            string fileName = Path.Combine(woxDirectory, "PythonHome\\pythonw.exe");
            string parameters = string.Format("{0} \"{1}\"", context.CurrentPluginMetadata.ExecuteFilePath,
                     string.Format(@"{{\""method\"": \""query\"", \""parameters\"": \""{0}\""}}",query.GetAllRemainingParameter()));
             
            return Execute(fileName, parameters);
        }

        protected override string ExecuteAction(JsonRPCRequestModel rpcRequest)
        {
            string fileName = Path.Combine(woxDirectory, "PythonHome\\pythonw.exe");
            string parameters = string.Format("{0} \"{1}\"", context.CurrentPluginMetadata.ExecuteFilePath,rpcRequest);
            return Execute(fileName, parameters);
        }
    }
}