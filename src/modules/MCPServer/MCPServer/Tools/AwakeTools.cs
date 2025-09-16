// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using ModelContextProtocol.Server;

namespace PowerToys.MCPServer.Tools
{
    [McpServerToolType]
    public static class AwakeTools
    {
        [McpServerTool]
        [Description("Echoes the message back to the client.")]
        public static string SetTimeTest(string message) => $"Hello {message}";
    }
}
