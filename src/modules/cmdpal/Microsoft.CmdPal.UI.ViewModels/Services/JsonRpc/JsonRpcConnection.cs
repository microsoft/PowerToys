// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Microsoft.CmdPal.UI.ViewModels.Services.JsonRpc;

/// <summary>
/// Manages JSON-RPC 2.0 communication with a Node.js child process over stdin/stdout using LSP-style length-prefixed framing.
/// </summary>
public sealed class JsonRpcConnection : IDisposable
{
    private readonly Process _process;
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<int, TaskCompletionSource<JsonRpcResponse>> _pendingRequests = new();
    private readonly ConcurrentDictionary<string, Action<JsonElement>> _notificationHandlers = new();
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly Lock _idLock = new();
    private readonly CancellationTokenSource _disposalCts = new();
    private readonly TimeSpan _defaultTimeout = TimeSpan.FromSeconds(10);

    private int _nextRequestId = 1;
    private Task? _readLoopTask;
    private bool _disposed;

    /// <summary>
    /// Raised when an error occurs during reading or processing messages.
    /// </summary>
    public event Action<Exception>? OnError;

    /// <summary>
    /// Raised when the connection is disconnected (process exit or stream close).
    /// </summary>
    public event Action? OnDisconnected;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonRpcConnection"/> class.
    /// </summary>
    /// <param name="process">The Node.js child process to communicate with.</param>
    /// <param name="logger">Logger for diagnostics.</param>
    public JsonRpcConnection(Process process, ILogger logger)
    {
        _process = process ?? throw new ArgumentNullException(nameof(process));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Starts listening for messages from the process stdout on a background thread.
    /// </summary>
    public void StartListening()
    {
        if (_readLoopTask != null)
        {
            throw new InvalidOperationException("Already listening");
        }

        _readLoopTask = Task.Run(ReadLoopAsync, _disposalCts.Token);
    }

    /// <summary>
    /// Sends a JSON-RPC request and waits for the response.
    /// </summary>
    /// <param name="method">The method name to invoke.</param>
    /// <param name="params">Optional parameters for the method.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The JSON-RPC response.</returns>
    public async Task<JsonRpcResponse> SendRequestAsync(string method, JsonNode? @params, CancellationToken ct)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(JsonRpcConnection));
        }

        int id;
        lock (_idLock)
        {
            id = _nextRequestId++;
        }

        var request = new JsonRpcRequest
        {
            Id = id,
            Method = method,
            Params = @params,
        };

        var tcs = new TaskCompletionSource<JsonRpcResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingRequests[id] = tcs;

        try
        {
            await SendMessageAsync(request, ct).ConfigureAwait(false);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct, _disposalCts.Token);
            timeoutCts.CancelAfter(_defaultTimeout);

            using (timeoutCts.Token.Register(() => tcs.TrySetCanceled()))
            {
                return await tcs.Task.ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            _pendingRequests.TryRemove(id, out _);
            throw;
        }
    }

    /// <summary>
    /// Sends a JSON-RPC notification (no response expected).
    /// </summary>
    /// <param name="method">The method name.</param>
    /// <param name="params">Optional parameters.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task SendNotificationAsync(string method, JsonNode? @params, CancellationToken ct)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(JsonRpcConnection));
        }

        var notification = new JsonRpcNotification
        {
            Method = method,
            Params = @params,
        };

        await SendMessageAsync(notification, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Registers a handler for incoming notifications of a specific method.
    /// </summary>
    /// <param name="method">The notification method name.</param>
    /// <param name="handler">The handler to invoke when the notification is received.</param>
    public void RegisterNotificationHandler(string method, Action<JsonElement> handler)
    {
        _notificationHandlers[method] = handler;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _disposalCts.Cancel();

        try
        {
            _readLoopTask?.Wait(TimeSpan.FromSeconds(2));
        }
        catch
        {
            // Best effort
        }

        foreach (var kvp in _pendingRequests)
        {
            kvp.Value.TrySetCanceled();
        }

        _pendingRequests.Clear();
        _writeLock.Dispose();
        _disposalCts.Dispose();
    }

    private async Task SendMessageAsync(object message, CancellationToken ct)
    {
        var json = message switch
        {
            JsonRpcRequest req => JsonSerializer.Serialize(req, JsonRpcSerializerContext.Default.JsonRpcRequest),
            JsonRpcNotification notif => JsonSerializer.Serialize(notif, JsonRpcSerializerContext.Default.JsonRpcNotification),
            _ => throw new ArgumentException("Invalid message type", nameof(message)),
        };

        var contentBytes = Encoding.UTF8.GetBytes(json);
        var header = $"Content-Length: {contentBytes.Length}\r\n\r\n";
        var headerBytes = Encoding.ASCII.GetBytes(header);

        await _writeLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var stdin = _process.StandardInput.BaseStream;
            await stdin.WriteAsync(headerBytes, 0, headerBytes.Length, ct).ConfigureAwait(false);
            await stdin.WriteAsync(contentBytes, 0, contentBytes.Length, ct).ConfigureAwait(false);
            await stdin.FlushAsync(ct).ConfigureAwait(false);

            _logger.LogDebug("Sent {MessageType}: {Json}", message.GetType().Name, json);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private async Task ReadLoopAsync()
    {
        try
        {
            var stdout = _process.StandardOutput.BaseStream;
            var headerBuffer = new byte[1024];
            var contentBuffer = new byte[65536];

            while (!_disposalCts.Token.IsCancellationRequested && !_process.HasExited)
            {
                // Read until we find "Content-Length: N\r\n\r\n"
                var contentLength = await ReadContentLengthAsync(stdout, headerBuffer, _disposalCts.Token).ConfigureAwait(false);
                if (contentLength <= 0)
                {
                    break;
                }

                // Ensure buffer is large enough
                if (contentLength > contentBuffer.Length)
                {
                    contentBuffer = new byte[contentLength];
                }

                // Read exactly contentLength bytes
                var totalRead = 0;
                while (totalRead < contentLength)
                {
                    var read = await stdout.ReadAsync(contentBuffer, totalRead, contentLength - totalRead, _disposalCts.Token).ConfigureAwait(false);
                    if (read == 0)
                    {
                        _logger.LogWarning("Stream closed before reading full message");
                        return;
                    }

                    totalRead += read;
                }

                var json = Encoding.UTF8.GetString(contentBuffer, 0, contentLength);
                _logger.LogDebug("Received message: {Json}", json);

                ProcessMessage(json);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during disposal
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in read loop");
            OnError?.Invoke(ex);
        }
        finally
        {
            OnDisconnected?.Invoke();
        }
    }

    private async Task<int> ReadContentLengthAsync(Stream stream, byte[] buffer, CancellationToken ct)
    {
        var position = 0;
        var headerComplete = false;

        while (!headerComplete && position < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer, position, 1, ct).ConfigureAwait(false);
            if (read == 0)
            {
                return -1; // Stream closed
            }

            position++;

            // Check for \r\n\r\n (end of headers)
            if (position >= 4 &&
                buffer[position - 4] == '\r' &&
                buffer[position - 3] == '\n' &&
                buffer[position - 2] == '\r' &&
                buffer[position - 1] == '\n')
            {
                headerComplete = true;
            }
        }

        if (!headerComplete)
        {
            _logger.LogError("Header too large or malformed");
            return -1;
        }

        var headerText = Encoding.ASCII.GetString(buffer, 0, position);
        var lines = headerText.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
            {
                var valueStr = line.Substring(15).Trim();
                if (int.TryParse(valueStr, out var length))
                {
                    return length;
                }
            }
        }

        _logger.LogError("Content-Length header not found");
        return -1;
    }

    private void ProcessMessage(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("id", out var idProp) && idProp.ValueKind == JsonValueKind.Number)
            {
                // This is a response
                var id = idProp.GetInt32();
                var response = JsonSerializer.Deserialize(json, JsonRpcSerializerContext.Default.JsonRpcResponse);

                if (response != null && _pendingRequests.TryRemove(id, out var tcs))
                {
                    tcs.SetResult(response);
                }
                else
                {
                    _logger.LogWarning("Received response for unknown request ID: {Id}", id);
                }
            }
            else if (root.TryGetProperty("method", out var methodProp))
            {
                // This is a notification
                var method = methodProp.GetString() ?? string.Empty;

                if (_notificationHandlers.TryGetValue(method, out var handler))
                {
                    var paramsElement = root.TryGetProperty("params", out var p) ? p : default;
                    handler(paramsElement);
                }
                else
                {
                    _logger.LogDebug("No handler registered for notification: {Method}", method);
                }
            }
            else
            {
                _logger.LogWarning("Received message with neither id nor method");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message: {Json}", json);
            OnError?.Invoke(ex);
        }
    }
}
