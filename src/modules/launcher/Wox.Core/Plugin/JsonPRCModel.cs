
/* We basically follow the Json-RPC 2.0 spec (http://www.jsonrpc.org/specification) to invoke methods between Wox and other plugins, 
 * like python or other self-execute program. But, we added addtional infos (proxy and so on) into rpc request. Also, we didn't use the
 * "id" and "jsonrpc" in the request, since it's not so useful in our request model.
 * 
 * When execute a query:
 *      Wox -------JsonRPCServerRequestModel--------> client
 *      Wox <------JsonRPCQueryResponseModel--------- client
 *      
 * When execute a action (which mean user select an item in reulst item):
 *      Wox -------JsonRPCServerRequestModel--------> client
 *      Wox <------JsonRPCResponseModel-------------- client
 * 
 */

using System.Collections.Generic;
using System.Linq;
using Wox.Plugin;

namespace Wox.Core.Plugin
{
    public class JsonRPCErrorModel
    {
        public int Code { get; set; }

        public string Message { get; set; }

        public string Data { get; set; }
    }

    public class JsonRPCModelBase
    {
        public int Id { get; set; }
    }

    public class JsonRPCResponseModel : JsonRPCModelBase
    {
        public string Result { get; set; }

        public JsonRPCErrorModel Error { get; set; }
    }

    public class JsonRPCQueryResponseModel : JsonRPCResponseModel
    {
        public new List<JsonRPCResult> Result { get; set; }
    }

    public class JsonRPCRequestModel : JsonRPCModelBase
    {
        public string Method { get; set; }

        public object[] Parameters { get; set; }

        public override string ToString()
        {
            string rpc = string.Empty;
            if (Parameters != null && Parameters.Length > 0)
            {
                string parameters = Parameters.Aggregate("[", (current, o) => current + (GetParameterByType(o) + ","));
                parameters = parameters.Substring(0, parameters.Length - 1) + "]";
                rpc = string.Format(@"{{\""method\"":\""{0}\"",\""parameters\"":{1}", Method, parameters);
            }
            else
            {
                rpc = string.Format(@"{{\""method\"":\""{0}\"",\""parameters\"":[]", Method);
            }

            return rpc;

        }

        private string GetParameterByType(object parameter)
        {
            if (parameter == null) {
                return "null";
            }
            if (parameter is string)
            {
                return string.Format(@"\""{0}\""", ReplaceEscapes(parameter.ToString()));
            }
            if (parameter is int || parameter is float || parameter is double)
            {
                return string.Format(@"{0}", parameter);
            }
            if (parameter is bool)
            {
                return string.Format(@"{0}", parameter.ToString().ToLower());
            }
            return parameter.ToString();
        }

        private string ReplaceEscapes(string str)
        {
            return str.Replace(@"\", @"\\") //Escapes in ProcessStartInfo
                .Replace(@"\", @"\\") //Escapes itself when passed to client
                .Replace(@"""", @"\\""""");
        }
    }

    /// <summary>
    /// Json RPC Request that Wox sent to client
    /// </summary>
    public class JsonRPCServerRequestModel : JsonRPCRequestModel
    {
        public override string ToString()
        {
            string rpc = base.ToString();
            return rpc + "}";
        }
    }

    /// <summary>
    /// Json RPC Request(in query response) that client sent to Wox
    /// </summary>
    public class JsonRPCClientRequestModel : JsonRPCRequestModel
    {
        public bool DontHideAfterAction { get; set; }

        public override string ToString()
        {
            string rpc = base.ToString();
            return rpc + "}";
        }
    }

    /// <summary>
    /// Represent the json-rpc result item that client send to Wox
    /// Typically, we will send back this request model to client after user select the result item
    /// But if the request method starts with "Wox.", we will invoke the public APIs we expose.
    /// </summary>
    public class JsonRPCResult : Result
    {
        public JsonRPCClientRequestModel JsonRPCAction { get; set; }
    }
}
