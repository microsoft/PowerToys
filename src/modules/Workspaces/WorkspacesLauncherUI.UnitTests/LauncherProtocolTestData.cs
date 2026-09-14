// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Buffers.Binary;
using System.Text;
using System.Text.Json;

namespace WorkspacesLauncherUI.UnitTests
{
    internal static class LauncherProtocolTestData
    {
        internal const string RequestId = "{01234567-89ab-cdef-0123-456789abcdef}";
        internal const string Shutdown = """{"protocolVersion":1,"type":"shutdown"}""";
        internal const string Dismiss = """{"protocolVersion":1,"type":"dismiss-warning","requestId":"{01234567-89ab-cdef-0123-456789abcdef}"}""";
        internal const string Warning = """
            {"protocolVersion":1,"type":"elevation-warning","requestId":"{01234567-89ab-cdef-0123-456789abcdef}",
             "appName":"Example","path":"C:\\Example\\app.exe","arguments":"--example","reason":"unsigned","status":"untrusted"}
            """;

        internal static string LaunchStatus(int processId, int state = 0)
        {
            return $$$$"""
                {"protocolVersion":1,"type":"launch-status","processId":{{{{processId}}}},"apps":{"appLaunchInfos":[
                  {"state":{{{{state}}}},"application":{"application":"Example","application-path":"C:\\Example\\app.exe",
                    "title":"Example title","package-full-name":"","app-user-model-id":"","pwa-app-id":"",
                    "command-line-arguments":"--example","is-elevated":false,"can-launch-elevated":true,
                    "minimized":false,"maximized":true,"monitor":2,"position":{"X":-100,"Y":200,"width":800,"height":600}}}]}}
                """;
        }

        internal static byte[] Frame(string json)
        {
            return Frame(Encoding.UTF8.GetBytes(json));
        }

        internal static byte[] Frame(byte[] body)
        {
            var frame = new byte[sizeof(uint) + body.Length];
            BinaryPrimitives.WriteUInt32LittleEndian(frame, (uint)body.Length);
            body.CopyTo(frame, sizeof(uint));
            return frame;
        }

        internal static string ExpectedSend(string type, string choice = null)
        {
            return type switch
            {
                "ready" or "cancel" => JsonSerializer.Serialize(new { protocolVersion = 1, type }),
                "elevation-response" => JsonSerializer.Serialize(new { protocolVersion = 1, type, requestId = RequestId, choice }),
                _ => JsonSerializer.Serialize(new { protocolVersion = 1, type, requestId = RequestId }),
            };
        }
    }
}
