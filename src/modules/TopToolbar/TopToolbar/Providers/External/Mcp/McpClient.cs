// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using TopToolbar.Logging;

namespace TopToolbar.Providers.External.Mcp;

internal sealed class McpClient : IDisposable
{
    private readonly ExternalActionProviderHost _host;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly ConcurrentDictionary<long, TaskCompletionSource<JsonElement>> _pending;
    private readonly SemaphoreSlim _connectLock = new(1, 1);
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly CancellationTokenSource _lifetimeCts = new();

    private StreamWriter _writer;
    private StreamReader _reader;
    private CancellationTokenSource _readerCts;
    private Task _readerTask;
    private long _nextId;
    private bool _initialized;
    private bool _disposed;

    public McpClient(ExternalActionProviderHost host)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _pending = new ConcurrentDictionary<long, TaskCompletionSource<JsonElement>>();
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        };
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);
        if (_initialized)
        {
            return;
        }

        var initParams = new
        {
            protocolVersion = "2024-11-05",
            capabilities = new { tools = new { }, resources = new { } },
            clientInfo = new { name = "TopToolbar", version = "1.0.0" },
        };

        await SendRequestAsync("initialize", initParams, cancellationToken).ConfigureAwait(false);
        await SendNotificationAsync("notifications/initialized", new { }, cancellationToken).ConfigureAwait(false);
        _initialized = true;
    }

    public async Task<IReadOnlyList<McpToolInfo>> ListToolsAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        var result = await SendRequestAsync("tools/list", null, cancellationToken).ConfigureAwait(false);
        var tools = new List<McpToolInfo>();
        if (result.TryGetProperty("tools", out var toolsElement) && toolsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var toolElement in toolsElement.EnumerateArray())
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!toolElement.TryGetProperty("name", out var nameElement))
                {
                    continue;
                }

                var name = nameElement.GetString();
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                string description = string.Empty;
                if (toolElement.TryGetProperty("description", out var descriptionElement))
                {
                    description = descriptionElement.GetString() ?? string.Empty;
                }

                JsonElement inputSchema = default;
                if (toolElement.TryGetProperty("inputSchema", out var schemaElement))
                {
                    inputSchema = schemaElement.Clone();
                }

                tools.Add(new McpToolInfo(name.Trim(), description.Trim(), inputSchema));
            }
        }

        return tools;
    }

    public Task<JsonElement> CallToolAsync(string name, JsonElement? arguments, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Tool name must be provided.", nameof(name));
        }

        ThrowIfDisposed();

        object payload = new
        {
            name,
            arguments = arguments.HasValue ? (object)arguments.Value : new JsonObject(),
        };

        return SendRequestAsync("tools/call", payload, cancellationToken);
    }

    private async Task EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        if (_writer != null && _host.IsRunning)
        {
            return;
        }

        await _connectLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_writer != null && _host.IsRunning)
            {
                return;
            }

            await _host.StartAsync(cancellationToken).ConfigureAwait(false);
            _writer = _host.StandardInput;
            _reader = _host.StandardOutput;

            _readerCts?.Cancel();
            _readerCts?.Dispose();
            _readerCts = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeCts.Token);
            _readerTask = Task.Run(() => ReadLoopAsync(_readerCts.Token), CancellationToken.None);
        }
        finally
        {
            _connectLock.Release();
        }
    }

    private async Task SendNotificationAsync(string method, object parameters, CancellationToken cancellationToken)
    {
        await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);

        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var notification = new JsonRpcRequest
            {
                JsonRpc = "2.0",
                Id = null,
                Method = method,
                Params = parameters,
            };

            var json = JsonSerializer.Serialize(notification, _jsonOptions);
            await _writer.WriteLineAsync(json).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private async Task<JsonElement> SendRequestAsync(string method, object parameters, CancellationToken cancellationToken)
    {
        await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);

        var id = Interlocked.Increment(ref _nextId);
        var tcs = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[id] = tcs;

        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var request = new JsonRpcRequest
            {
                JsonRpc = "2.0",
                Id = id,
                Method = method,
                Params = parameters,
            };

            var json = JsonSerializer.Serialize(request, _jsonOptions);
            await _writer.WriteLineAsync(json).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }

        using (cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken)))
        {
            return await tcs.Task.ConfigureAwait(false);
        }
    }

    private async Task ReadLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await _reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                if (line == null)
                {
                    break;
                }

                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                try
                {
                    using var document = JsonDocument.Parse(line);
                    var root = document.RootElement;

                    if (!root.TryGetProperty("id", out var idElement) || idElement.ValueKind == JsonValueKind.Null)
                    {
                        continue;
                    }

                    if (!TryParseId(idElement, out var id))
                    {
                        continue;
                    }

                    if (!_pending.TryRemove(id, out var tcs))
                    {
                        continue;
                    }

                    if (root.TryGetProperty("error", out var errorElement) && errorElement.ValueKind != JsonValueKind.Null)
                    {
                        var error = McpError.FromJson(errorElement);
                        tcs.TrySetException(new McpException(error.Code, error.Message, errorElement.Clone()));
                    }
                    else if (root.TryGetProperty("result", out var resultElement))
                    {
                        tcs.TrySetResult(resultElement.Clone());
                    }
                    else
                    {
                        tcs.TrySetResult(default);
                    }
                }
                catch (JsonException ex)
                {
                    AppLogger.LogWarning($"McpClient: failed to parse MCP response - {ex.Message}.");
                }
            }
        }
        catch (ObjectDisposedException)
        {
            // Shutdown in progress
        }
        catch (Exception ex)
        {
            AppLogger.LogWarning($"McpClient: read loop failed - {ex.Message}.");
        }
        finally
        {
            foreach (var pending in _pending)
            {
                if (_pending.TryRemove(pending.Key, out var tcs))
                {
                    tcs.TrySetException(new IOException("MCP connection closed."));
                }
            }

            _writer = null;
            _reader = null;
            _initialized = false;
        }
    }

    private static bool TryParseId(JsonElement element, out long id)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Number:
                if (element.TryGetInt64(out var value))
                {
                    id = value;
                    return true;
                }

                break;
            case JsonValueKind.String:
                if (long.TryParse(element.GetString(), out var parsed))
                {
                    id = parsed;
                    return true;
                }

                break;
        }

        id = 0;
        return false;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(McpClient));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _lifetimeCts.Cancel();

        try
        {
            _readerCts?.Cancel();
            _readerCts?.Dispose();
        }
        catch
        {
        }

        try
        {
            _readerTask?.Wait(TimeSpan.FromSeconds(1));
        }
        catch
        {
        }

        _writeLock.Dispose();
        _connectLock.Dispose();
        _lifetimeCts.Dispose();
    }

    private sealed class JsonRpcRequest
    {
        public string JsonRpc { get; set; }

        public long? Id { get; set; }

        public string Method { get; set; }

        public object Params { get; set; }
    }
}
