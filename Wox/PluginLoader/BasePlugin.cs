using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Wox.JsonRPC;
using Wox.Plugin;

namespace Wox.PluginLoader
{
    public abstract class BasePlugin : IPlugin
    {
        protected PluginInitContext context;

        public abstract string SupportedLanguage { get; }

        protected abstract string ExecuteQuery(Query query);
        protected abstract string ExecuteAction(JsonRPCRequestModel rpcRequest);

        public List<Result> Query(Query query)
        {
            string output = ExecuteQuery(query);
            if (!string.IsNullOrEmpty(output))
            {
                try
                {
                    List<Result> results = new List<Result>();

                    JsonRPCQueryResponseModel queryResponseModel = JsonConvert.DeserializeObject<JsonRPCQueryResponseModel>(output);
                    foreach (JsonRPCResult result in queryResponseModel.Result)
                    {
                        JsonRPCResult result1 = result;
                        result.Action = (c) =>
                        {
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
                                        string actionReponse = ExecuteAction(result1.JsonRPCAction);
                                        JsonRPCRequestModel jsonRpcRequestModel = JsonConvert.DeserializeObject<JsonRPCRequestModel>(actionReponse);
                                        if (jsonRpcRequestModel != null 
                                            && string.IsNullOrEmpty(jsonRpcRequestModel.Method) 
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
                catch
                {
                }
            }
            return null;
        }

        private void ExecuteWoxAPI(string method, object[] parameters)
        {
            MethodInfo methodInfo = App.Window.GetType().GetMethod(method);
            if (methodInfo != null)
            {
                try
                {
                    methodInfo.Invoke(App.Window, parameters);
                }
                catch (Exception)
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
