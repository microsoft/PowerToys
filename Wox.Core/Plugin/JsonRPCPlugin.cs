using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using Newtonsoft.Json;
using Wox.Core.Exception;
using Wox.Infrastructure.Logger;
using Wox.Plugin;

namespace Wox.Core.Plugin
{
    /// <summary>
    /// Represent the plugin that using JsonPRC
    /// </summary>
    internal abstract class JsonRPCPlugin : IPlugin
    {
        protected PluginInitContext context;

        /// <summary>
        /// The language this JsonRPCPlugin support
        /// </summary>
        public abstract string SupportedLanguage { get; }

        protected abstract string ExecuteQuery(Query query);
        protected abstract string ExecuteCallback(JsonRPCRequestModel rpcRequest);

        public List<Result> Query(Query query)
        {
            string output = ExecuteQuery(query);
            if (!string.IsNullOrEmpty(output))
            {
                try
                {
                    List<Result> results = new List<Result>();

                    JsonRPCQueryResponseModel queryResponseModel = JsonConvert.DeserializeObject<JsonRPCQueryResponseModel>(output);
                    if (queryResponseModel.Result == null) return null;

                    foreach (JsonRPCResult result in queryResponseModel.Result)
                    {
                        JsonRPCResult result1 = result;
                        result.Action = (c) =>
                        {
                            if (result1.JsonRPCAction == null) return false;

                            if (!string.IsNullOrEmpty(result1.JsonRPCAction.Method))
                            {
                                if (result1.JsonRPCAction.Method.StartsWith("Wox."))
                                {
                                    ExecuteWoxAPI(result1.JsonRPCAction.Method.Substring(4), result1.JsonRPCAction.Parameters);
                                }
                                else
                                {
                                    ThreadPool.QueueUserWorkItem(state =>
                                    {
                                        string actionReponse = ExecuteCallback(result1.JsonRPCAction);
                                        JsonRPCRequestModel jsonRpcRequestModel = JsonConvert.DeserializeObject<JsonRPCRequestModel>(actionReponse);
                                        if (jsonRpcRequestModel != null
                                            && !string.IsNullOrEmpty(jsonRpcRequestModel.Method)
                                            && jsonRpcRequestModel.Method.StartsWith("Wox."))
                                        {
                                            ExecuteWoxAPI(jsonRpcRequestModel.Method.Substring(4), jsonRpcRequestModel.Parameters);
                                        }
                                    });
                                }
                            }
                            return !result1.JsonRPCAction.DontHideAfterAction;
                        };
                        results.Add(result);
                    }
                    return results;
                }
                catch (System.Exception e)
                {
                    Log.Error(e);
                }
            }
            return null;
        }

        private void ExecuteWoxAPI(string method, object[] parameters)
        {
            MethodInfo methodInfo = PluginManager.API.GetType().GetMethod(method);
            if (methodInfo != null)
            {
                try
                {
                    methodInfo.Invoke(PluginManager.API, parameters);
                }
                catch (System.Exception)
                {
#if (DEBUG)
                    {
                        throw;
                    }
#endif
                }
            }
        }

        /// <summary>
        /// Execute external program and return the output
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="arguments"></param>
        /// <returns></returns>
        protected string Execute(string fileName, string arguments)
        {
            ProcessStartInfo start = new ProcessStartInfo();
            start.FileName = fileName;
            start.Arguments = arguments;
            start.UseShellExecute = false;
            start.CreateNoWindow = true;
            start.RedirectStandardOutput = true;
            start.RedirectStandardError = true;
            return Execute(start);
        }

        protected string Execute(ProcessStartInfo startInfo)
        {
            try
            {
                using (Process process = Process.Start(startInfo))
                {
                    if (process != null)
                    {
                        using (StreamReader reader = process.StandardOutput)
                        {
                            string result = reader.ReadToEnd();
                            if (result.StartsWith("DEBUG:"))
                            {
                                MessageBox.Show(new Form { TopMost = true }, result.Substring(6));
                                return "";
                            }
                            if (string.IsNullOrEmpty(result))
                            {
                                using (StreamReader errorReader = process.StandardError)
                                {
                                    string error = errorReader.ReadToEnd();
                                    if (!string.IsNullOrEmpty(error))
                                    {
                                        throw new WoxJsonRPCException(error);
                                    }
                                }
                            }
                            return result;
                        }
                    }
                }
            }
            catch(System.Exception e)
            {
                throw new WoxJsonRPCException(e.Message);
            }
            return null;
        }

        public void Init(PluginInitContext ctx)
        {
            context = ctx;
        }
    }
}
