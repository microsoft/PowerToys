// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.CmdPal.Ext.PowerToys.Helper;

internal static class PowerToysRpcClient
{
    private const string PipeName = "PowerToys.CmdPal.Rpc";

    public static bool TryInvoke(string module, string method, object? parameters = null, int timeoutMs = 5000)
    {
        var request = new RpcRequest
        {
            Module = module,
            Method = method,
            Parameters = parameters ?? new { },
            TimeoutMs = timeoutMs,
        };

        try
        {
            using var pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut, PipeOptions.None);
            pipe.Connect(timeoutMs);

            var payload = JsonSerializer.SerializeToUtf8Bytes(request, PowerToysRpcClientContext.Default.RpcRequest);
            using (var writer = new BinaryWriter(pipe, Encoding.UTF8, leaveOpen: true))
            {
                writer.Write(payload.Length);
                writer.Flush();
            }

            pipe.Write(payload, 0, payload.Length);
            pipe.Flush();

            using var reader = new BinaryReader(pipe, Encoding.UTF8, leaveOpen: true);
            var responseLength = reader.ReadInt32();
            if (responseLength <= 0)
            {
                return false;
            }

            var buffer = new byte[responseLength];
            var totalRead = 0;
            while (totalRead < responseLength)
            {
                var read = pipe.Read(buffer, totalRead, responseLength - totalRead);
                if (read == 0)
                {
                    return false;
                }

                totalRead += read;
            }

            using var document = JsonDocument.Parse(buffer);
            return document.RootElement.TryGetProperty("ok", out var okProperty) && okProperty.GetBoolean();
        }
        catch
        {
            return false;
        }
    }

    internal sealed class RpcRequest
    {
        [JsonPropertyName("version")]
        public string Version { get; init; } = "1.0";

        [JsonPropertyName("id")]
        public string Id { get; init; } = Guid.NewGuid().ToString();

        [JsonPropertyName("module")]
        public string Module { get; init; } = string.Empty;

        [JsonPropertyName("method")]
        public string Method { get; init; } = string.Empty;

        [JsonPropertyName("params")]
        public object Parameters { get; init; } = new { };

        [JsonPropertyName("timeoutMs")]
        public int TimeoutMs { get; init; }
    }
}
