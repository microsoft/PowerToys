// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.UI.ViewModels.Services.JsonRpc;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

/// <summary>
/// In-memory fake Node.js extension. It drives a real <see cref="JsonRpcConnection"/>
/// over paired pipes, answering requests with canned JSON responses and pushing
/// notifications on demand. No external process is started.
/// </summary>
internal sealed class JSFakeExtension : IDisposable
{
    private readonly Pipe _toHost = new();
    private readonly Pipe _fromHost = new();
    private readonly Stream _extensionReads;
    private readonly Stream _extensionWrites;
    private readonly ConcurrentDictionary<string, Func<JsonElement, JsonNode?>> _handlers = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _pump;
    private bool _isDisposed;

    public JSFakeExtension()
    {
        Connection = new JsonRpcConnection(_toHost.Reader.AsStream(), _fromHost.Writer.AsStream());
        Connection.StartListening();

        _extensionReads = _fromHost.Reader.AsStream();
        _extensionWrites = _toHost.Writer.AsStream();
        _pump = Task.Run(() => PumpAsync(_cts.Token));
    }

    public JsonRpcConnection Connection { get; }

    public void OnRequest(string method, Func<JsonElement, JsonNode?> handler) => _handlers[method] = handler;

    public void OnResult(string method, string resultJson) => _handlers[method] = _ => JsonNode.Parse(resultJson);

    public async Task PushNotificationAsync(string method, JsonNode? parameters)
    {
        var message = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["method"] = method,
        };

        if (parameters is not null)
        {
            message["params"] = parameters;
        }

        await WriteFramedAsync(message.ToJsonString(), _cts.Token);
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;

        try
        {
            _cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        Connection.Dispose();
        _cts.Dispose();
    }

    private async Task PumpAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var body = await ReadFramedAsync(_extensionReads, cancellationToken);
                using var document = JsonDocument.Parse(body);
                var root = document.RootElement;

                if (!root.TryGetProperty("id", out var idProp) || idProp.ValueKind != JsonValueKind.Number)
                {
                    continue;
                }

                var id = idProp.GetInt32();
                var method = root.TryGetProperty("method", out var methodProp) ? methodProp.GetString() ?? string.Empty : string.Empty;
                var parameters = root.TryGetProperty("params", out var paramsProp) ? paramsProp.Clone() : default;

                JsonNode? result = null;
                if (_handlers.TryGetValue(method, out var handler))
                {
                    result = handler(parameters);
                }

                await RespondAsync(id, result, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (EndOfStreamException)
        {
        }
        catch (IOException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private async Task RespondAsync(int id, JsonNode? result, CancellationToken cancellationToken)
    {
        var message = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["result"] = result,
        };

        await WriteFramedAsync(message.ToJsonString(), cancellationToken);
    }

    private async Task WriteFramedAsync(string json, CancellationToken cancellationToken)
    {
        var body = Encoding.UTF8.GetBytes(json);
        var header = Encoding.ASCII.GetBytes($"Content-Length: {body.Length}\r\n\r\n");
        var buffer = new byte[header.Length + body.Length];
        Buffer.BlockCopy(header, 0, buffer, 0, header.Length);
        Buffer.BlockCopy(body, 0, buffer, header.Length, body.Length);

        await _extensionWrites.WriteAsync(buffer, cancellationToken);
        await _extensionWrites.FlushAsync(cancellationToken);
    }

    private static async Task<string> ReadFramedAsync(Stream stream, CancellationToken cancellationToken)
    {
        var headerBytes = new List<byte>(64);
        var single = new byte[1];

        while (true)
        {
            var read = await stream.ReadAsync(single.AsMemory(0, 1), cancellationToken);
            if (read == 0)
            {
                throw new EndOfStreamException("Stream closed before a full header was read.");
            }

            headerBytes.Add(single[0]);
            var count = headerBytes.Count;
            if (count >= 4 &&
                headerBytes[count - 4] == (byte)'\r' &&
                headerBytes[count - 3] == (byte)'\n' &&
                headerBytes[count - 2] == (byte)'\r' &&
                headerBytes[count - 1] == (byte)'\n')
            {
                break;
            }
        }

        var header = Encoding.ASCII.GetString(headerBytes.ToArray());
        var contentLength = ParseContentLength(header);

        var payload = new byte[contentLength];
        var offset = 0;
        while (offset < contentLength)
        {
            var read = await stream.ReadAsync(payload.AsMemory(offset, contentLength - offset), cancellationToken);
            if (read == 0)
            {
                throw new EndOfStreamException("Stream closed before the full body was read.");
            }

            offset += read;
        }

        return Encoding.UTF8.GetString(payload);
    }

    private static int ParseContentLength(string header)
    {
        foreach (var line in header.Split("\r\n", StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = line.IndexOf(':', StringComparison.Ordinal);
            if (separator <= 0)
            {
                continue;
            }

            if (line.AsSpan(0, separator).Trim().Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
            {
                return int.Parse(line.AsSpan(separator + 1).Trim(), System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        throw new FormatException("Missing Content-Length header.");
    }
}
