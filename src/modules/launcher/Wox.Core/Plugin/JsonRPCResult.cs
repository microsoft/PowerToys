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

using Wox.Plugin;

namespace Wox.Core.Plugin
{
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
