// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;
using ManagedCommon;

namespace Microsoft.CmdPal.UI.ViewModels.Services.JsonRpc;

/// <summary>
/// Low-level JSON-RPC 2.0 transport that speaks LSP-style Content-Length framing
/// over a pair of byte streams (typically a child process's stdout and stdin).
/// The transport is symmetric: it can send requests and notifications, and it
/// dispatches inbound requests and notifications to registered handlers.
/// </summary>
public sealed partial class JsonRpcConnection : IDisposable
{
    private const int MaxHeaderBytes = 16 * 1024;
    private const int MaxMessageBytes = 32 * 1024 * 1024;

    private readonly Stream _input;
    private readonly Stream _output;
    private readonly Stream? _errorStream;
    private readonly TimeSpan _requestTimeout;

    private readonly ConcurrentDictionary<int, TaskCompletionSource<JsonRpcResponse>> _pendingRequests = new();
    private readonly ConcurrentDictionary<string, Action<JsonElement>> _notificationHandlers = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, Func<JsonElement, CancellationToken, Task<JsonNode?>>> _requestHandlers = new(StringComparer.Ordinal);

    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly CancellationTokenSource _disposalCts = new();

    private int _nextRequestId;
    private Task? _readLoopTask;
    private Task? _errorPumpTask;
    private volatile bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonRpcConnection"/> class.
    /// </summary>
    /// <param name="input">The stream to read incoming framed messages from (for example, a process's standard output).</param>
    /// <param name="output">The stream to write outgoing framed messages to (for example, a process's standard input).</param>
    /// <param name="errorStream">An optional stream carrying out-of-band diagnostics (for example, a process's standard error). It is logged but is never part of the protocol.</param>
    /// <param name="requestTimeout">The per-request timeout. Defaults to 10 seconds when null.</param>
    public JsonRpcConnection(Stream input, Stream output, Stream? errorStream = null, TimeSpan? requestTimeout = null)
    {
        _input = input ?? throw new ArgumentNullException(nameof(input));
        _output = output ?? throw new ArgumentNullException(nameof(output));
        _errorStream = errorStream;
        _requestTimeout = requestTimeout ?? TimeSpan.FromSeconds(10);
    }

    /// <summary>
    /// Raised when the read loop ends because the underlying stream closed.
    /// </summary>
    public event EventHandler? Disconnected;

    /// <summary>
    /// Raised when the read loop encounters an unrecoverable protocol or stream error.
    /// </summary>
    public event EventHandler<JsonRpcErrorEventArgs>? Error;

    /// <summary>
    /// Starts the background read loop (and the optional stderr pump). Must be called once.
    /// </summary>
    public void StartListening()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_readLoopTask is not null)
        {
            throw new InvalidOperationException("The connection is already listening.");
        }

        _readLoopTask = Task.Run(ReadLoopAsync);

        if (_errorStream is not null)
        {
            _errorPumpTask = Task.Run(PumpErrorStreamAsync);
        }
    }

    /// <summary>
    /// Sends a JSON-RPC request and waits for the correlated response.
    /// </summary>
    /// <param name="method">The method name to invoke.</param>
    /// <param name="parameters">Optional parameters for the method.</param>
    /// <param name="cancellationToken">A token used to cancel the wait.</param>
    /// <returns>The raw JSON-RPC response, which may contain a result or an error.</returns>
    public async Task<JsonRpcResponse> SendRequestAsync(string method, JsonNode? parameters, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var id = Interlocked.Increment(ref _nextRequestId);
        var request = new JsonRpcRequest
        {
            Id = id,
            Method = method,
            Params = parameters,
        };

        var tcs = new TaskCompletionSource<JsonRpcResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingRequests[id] = tcs;

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _disposalCts.Token);
        timeoutCts.CancelAfter(_requestTimeout);

        try
        {
            var json = JsonSerializer.Serialize(request, JsonRpcSerializerContext.Default.JsonRpcRequest);
            await WriteFramedAsync(json, timeoutCts.Token).ConfigureAwait(false);

            using (timeoutCts.Token.Register(static state => ((TaskCompletionSource<JsonRpcResponse>)state!).TrySetCanceled(), tcs))
            {
                return await tcs.Task.ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            if (_disposed || _disposalCts.IsCancellationRequested)
            {
                throw new JsonRpcException("The JSON-RPC connection was closed before a response was received.");
            }

            throw new TimeoutException($"The JSON-RPC request '{method}' timed out after {_requestTimeout.TotalSeconds:0} seconds.");
        }
        finally
        {
            _pendingRequests.TryRemove(id, out _);
        }
    }

    /// <summary>
    /// Sends a JSON-RPC request and deserializes the successful result to <typeparamref name="TResult"/>.
    /// </summary>
    /// <typeparam name="TResult">The type to deserialize the result into.</typeparam>
    /// <param name="method">The method name to invoke.</param>
    /// <param name="parameters">Optional parameters for the method.</param>
    /// <param name="resultTypeInfo">The source-generated type metadata used to deserialize the result.</param>
    /// <param name="cancellationToken">A token used to cancel the wait.</param>
    /// <returns>The deserialized result, or the default value when the result is null.</returns>
    /// <exception cref="JsonRpcException">Thrown when the peer returns an error response.</exception>
    public async Task<TResult?> SendRequestAsync<TResult>(string method, JsonNode? parameters, JsonTypeInfo<TResult> resultTypeInfo, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(resultTypeInfo);

        var response = await SendRequestAsync(method, parameters, cancellationToken).ConfigureAwait(false);

        if (response.Error is not null)
        {
            throw new JsonRpcException(response.Error);
        }

        if (response.Result is not { } result || result.ValueKind == JsonValueKind.Null)
        {
            return default;
        }

        return result.Deserialize(resultTypeInfo);
    }

    /// <summary>
    /// Sends a JSON-RPC notification. Notifications never receive a response.
    /// </summary>
    /// <param name="method">The notification method name.</param>
    /// <param name="parameters">Optional parameters for the notification.</param>
    /// <param name="cancellationToken">A token used to cancel the write.</param>
    /// <returns>A task that completes when the notification has been written to the output stream.</returns>
    public async Task SendNotificationAsync(string method, JsonNode? parameters, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var notification = new JsonRpcNotification
        {
            Method = method,
            Params = parameters,
        };

        var json = JsonSerializer.Serialize(notification, JsonRpcSerializerContext.Default.JsonRpcNotification);
        await WriteFramedAsync(json, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Registers a handler for inbound notifications of a specific method. Replaces any existing handler.
    /// </summary>
    /// <param name="method">The notification method name.</param>
    /// <param name="handler">The handler invoked with the notification parameters.</param>
    public void RegisterNotificationHandler(string method, Action<JsonElement> handler)
    {
        ArgumentException.ThrowIfNullOrEmpty(method);
        ArgumentNullException.ThrowIfNull(handler);

        _notificationHandlers[method] = handler;
    }

    /// <summary>
    /// Registers a handler for inbound requests of a specific method. Replaces any existing handler.
    /// The handler returns the result payload, which is sent back as the response.
    /// </summary>
    /// <param name="method">The request method name.</param>
    /// <param name="handler">The handler invoked with the request parameters.</param>
    public void RegisterRequestHandler(string method, Func<JsonElement, CancellationToken, Task<JsonNode?>> handler)
    {
        ArgumentException.ThrowIfNullOrEmpty(method);
        ArgumentNullException.ThrowIfNull(handler);

        _requestHandlers[method] = handler;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        try
        {
            _disposalCts.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        FailAllPending("The JSON-RPC connection was disposed.");

        try
        {
            _readLoopTask?.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
        }

        try
        {
            _errorPumpTask?.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
        }

        _writeLock.Dispose();
        _disposalCts.Dispose();
    }

    private async Task WriteFramedAsync(string json, CancellationToken cancellationToken)
    {
        var body = Encoding.UTF8.GetBytes(json);
        var header = Encoding.ASCII.GetBytes($"Content-Length: {body.Length}\r\n\r\n");

        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _output.WriteAsync(header, cancellationToken).ConfigureAwait(false);
            await _output.WriteAsync(body, cancellationToken).ConfigureAwait(false);
            await _output.FlushAsync(cancellationToken).ConfigureAwait(false);
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
            while (!_disposalCts.IsCancellationRequested)
            {
                var contentLength = await ReadHeaderAsync(_disposalCts.Token).ConfigureAwait(false);
                if (contentLength < 0)
                {
                    break;
                }

                if (contentLength == 0)
                {
                    continue;
                }

                var body = await ReadExactAsync(contentLength, _disposalCts.Token).ConfigureAwait(false);
                if (body is null)
                {
                    break;
                }

                var json = Encoding.UTF8.GetString(body);
                DispatchMessage(json);
            }
        }
        catch (OperationCanceledException) when (_disposalCts.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            Logger.LogError("JSON-RPC read loop failed.", ex);
            RaiseError(ex);
        }
        finally
        {
            FailAllPending("The JSON-RPC connection was closed.");
            RaiseDisconnected();
        }
    }

    private async Task<int> ReadHeaderAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[MaxHeaderBytes];
        var position = 0;
        var single = new byte[1];

        while (true)
        {
            var read = await _input.ReadAsync(single.AsMemory(0, 1), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                if (position == 0)
                {
                    return -1;
                }

                throw new InvalidDataException("The stream closed in the middle of a JSON-RPC header.");
            }

            if (position >= buffer.Length)
            {
                throw new InvalidDataException("The JSON-RPC header exceeded the maximum allowed size.");
            }

            buffer[position] = single[0];
            position++;

            if (position >= 4 &&
                buffer[position - 4] == (byte)'\r' &&
                buffer[position - 3] == (byte)'\n' &&
                buffer[position - 2] == (byte)'\r' &&
                buffer[position - 1] == (byte)'\n')
            {
                break;
            }
        }

        var headerText = Encoding.ASCII.GetString(buffer, 0, position);
        foreach (var line in headerText.Split("\r\n", StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = line.IndexOf(':', StringComparison.Ordinal);
            if (separator <= 0)
            {
                continue;
            }

            var name = line.AsSpan(0, separator).Trim();
            if (name.Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
            {
                var value = line.AsSpan(separator + 1).Trim();
                if (!int.TryParse(value, out var length) || length < 0)
                {
                    throw new InvalidDataException("The JSON-RPC Content-Length header value was invalid.");
                }

                if (length > MaxMessageBytes)
                {
                    throw new InvalidDataException($"The JSON-RPC Content-Length {length} exceeds the maximum allowed message size of {MaxMessageBytes} bytes.");
                }

                return length;
            }
        }

        throw new InvalidDataException("The JSON-RPC message was missing a Content-Length header.");
    }

    private async Task<byte[]?> ReadExactAsync(int count, CancellationToken cancellationToken)
    {
        var buffer = new byte[count];
        var offset = 0;

        while (offset < count)
        {
            var read = await _input.ReadAsync(buffer.AsMemory(offset, count - offset), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return null;
            }

            offset += read;
        }

        return buffer;
    }

    private void DispatchMessage(string json)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            Logger.LogError($"Failed to parse an inbound JSON-RPC message: {json}", ex);
            RaiseError(ex);
            return;
        }

        using (document)
        {
            var root = document.RootElement;
            var hasId = root.TryGetProperty("id", out var idElement) && idElement.ValueKind != JsonValueKind.Null;
            var hasMethod = root.TryGetProperty("method", out var methodElement) && methodElement.ValueKind == JsonValueKind.String;

            if (hasMethod && !hasId)
            {
                DispatchNotification(methodElement.GetString() ?? string.Empty, root);
            }
            else if (hasMethod && hasId)
            {
                DispatchInboundRequest(methodElement.GetString() ?? string.Empty, idElement, root);
            }
            else if (hasId)
            {
                DispatchResponse(idElement, json);
            }
            else
            {
                Logger.LogWarning("Received a JSON-RPC message with neither a method nor an id.");
            }
        }
    }

    private void DispatchResponse(JsonElement idElement, string json)
    {
        if (idElement.ValueKind != JsonValueKind.Number || !idElement.TryGetInt32(out var id))
        {
            Logger.LogWarning("Received a JSON-RPC response with a non-integer id.");
            return;
        }

        JsonRpcResponse? response;
        try
        {
            response = JsonSerializer.Deserialize(json, JsonRpcSerializerContext.Default.JsonRpcResponse);
        }
        catch (JsonException ex)
        {
            Logger.LogError("Failed to deserialize a JSON-RPC response.", ex);
            RaiseError(ex);
            return;
        }

        if (response is null)
        {
            return;
        }

        if (_pendingRequests.TryRemove(id, out var tcs))
        {
            tcs.TrySetResult(response);
        }
        else
        {
            Logger.LogWarning($"Received a JSON-RPC response for an unknown request id {id}.");
        }
    }

    private void DispatchNotification(string method, JsonElement root)
    {
        if (!_notificationHandlers.TryGetValue(method, out var handler))
        {
            Logger.LogDebug($"No handler registered for JSON-RPC notification '{method}'.");
            return;
        }

        var parameters = root.TryGetProperty("params", out var p) ? p.Clone() : default;

        try
        {
            handler(parameters);
        }
        catch (Exception ex)
        {
            Logger.LogError($"The JSON-RPC notification handler for '{method}' threw an exception.", ex);
            RaiseError(ex);
        }
    }

    private void DispatchInboundRequest(string method, JsonElement idElement, JsonElement root)
    {
        var id = idElement.Clone();
        var parameters = root.TryGetProperty("params", out var p) ? p.Clone() : default;

        if (!_requestHandlers.TryGetValue(method, out var handler))
        {
            _ = SendErrorResponseAsync(id, JsonRpcError.MethodNotFound, $"The method '{method}' is not supported.");
            return;
        }

        _ = HandleInboundRequestAsync(method, id, parameters, handler);
    }

    private async Task HandleInboundRequestAsync(string method, JsonElement id, JsonElement parameters, Func<JsonElement, CancellationToken, Task<JsonNode?>> handler)
    {
        try
        {
            var result = await handler(parameters, _disposalCts.Token).ConfigureAwait(false);
            await SendResultResponseAsync(id, result).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_disposalCts.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            Logger.LogError($"The JSON-RPC request handler for '{method}' threw an exception.", ex);
            try
            {
                await SendErrorResponseAsync(id, JsonRpcError.InternalError, ex.Message).ConfigureAwait(false);
            }
            catch (Exception sendEx)
            {
                Logger.LogError("Failed to send a JSON-RPC error response.", sendEx);
            }
        }
    }

    private Task SendResultResponseAsync(JsonElement id, JsonNode? result)
    {
        var response = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = NodeFromElement(id),
            ["result"] = result,
        };

        return WriteFramedAsync(response.ToJsonString(), _disposalCts.Token);
    }

    private Task SendErrorResponseAsync(JsonElement id, int code, string message)
    {
        var response = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = NodeFromElement(id),
            ["error"] = new JsonObject
            {
                ["code"] = code,
                ["message"] = message,
            },
        };

        return WriteFramedAsync(response.ToJsonString(), _disposalCts.Token);
    }

    private static JsonNode? NodeFromElement(JsonElement element)
    {
        return element.ValueKind == JsonValueKind.Undefined ? null : JsonNode.Parse(element.GetRawText());
    }

    private async Task PumpErrorStreamAsync()
    {
        try
        {
            using var reader = new StreamReader(_errorStream!, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);
            while (!_disposalCts.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(_disposalCts.Token).ConfigureAwait(false);
                if (line is null)
                {
                    break;
                }

                if (!string.IsNullOrWhiteSpace(line))
                {
                    Logger.LogWarning($"[extension stderr] {line}");
                }
            }
        }
        catch (OperationCanceledException) when (_disposalCts.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            Logger.LogDebug($"The JSON-RPC stderr pump ended: {ex.Message}");
        }
    }

    private void FailAllPending(string message)
    {
        foreach (var key in _pendingRequests.Keys)
        {
            if (_pendingRequests.TryRemove(key, out var tcs))
            {
                tcs.TrySetException(new JsonRpcException(message));
            }
        }
    }

    private void RaiseDisconnected()
    {
        Disconnected?.Invoke(this, EventArgs.Empty);
    }

    private void RaiseError(Exception exception)
    {
        Error?.Invoke(this, new JsonRpcErrorEventArgs(exception));
    }
}
