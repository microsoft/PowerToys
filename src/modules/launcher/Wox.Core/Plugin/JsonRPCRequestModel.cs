// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/* We basically follow the Json-RPC 2.0 spec (http://www.jsonrpc.org/specification) to invoke methods between Wox and other plugins,
 * like python or other self-execute program. But, we added additional infos (proxy and so on) into rpc request. Also, we didn't use the
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

using System.Linq;

namespace Wox.Core.Plugin
{
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
            if (parameter == null)
            {
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
            return str.Replace(@"\", @"\\") // Escapes in ProcessStartInfo
                .Replace(@"\", @"\\") // Escapes itself when passed to client
                .Replace(@"""", @"\\""""");
        }
    }
}
