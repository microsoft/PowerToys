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
using System.Threading.Channels;
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

    // The connection has not been closed.
    private const int StateOpen = 0;

    // The connection has reached its terminal closed state: the reader exited, a write failed,
    // or the connection was disposed. No further protocol traffic is possible.
    private const int StateClosed = 1;

    // Upper bound on the number of inbound notifications buffered for the serialized consumer.
    // The reader never blocks on this queue: when it is full the oldest notification is dropped.
    private const int NotificationQueueCapacity = 1024;

    // Number of worker tasks that service inbound requests. This caps how many inbound request
    // handlers can run at once so a flood of inbound requests cannot spawn unbounded work.
    internal const int InboundRequestWorkerCount = 16;

    // Upper bound on the number of inbound requests buffered ahead of the workers. When the queue
    // is full the read loop blocks on the enqueue, applying backpressure to the peer rather than
    // buffering without limit.
    private const int InboundRequestQueueCapacity = 256;

    // Upper bound on how many characters of an offending payload are written to the log. Malformed
    // or oversized bodies are truncated so a single bad frame cannot flood the log up to the frame cap.
    internal const int MaxLoggedBodyChars = 1024;

    private readonly Stream _input;
    private readonly Stream _output;
    private readonly Stream? _errorStream;
    private readonly TimeSpan _requestTimeout;
    private readonly TimeSpan _writeTimeout;

    private readonly ConcurrentDictionary<int, TaskCompletionSource<JsonRpcResponse>> _pendingRequests = new();
    private readonly ConcurrentDictionary<string, Action<JsonElement>> _notificationHandlers = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, Func<JsonElement, CancellationToken, Task<JsonNode?>>> _requestHandlers = new(StringComparer.Ordinal);

    private readonly Channel<NotificationEnvelope> _notificationQueue = Channel.CreateBounded<NotificationEnvelope>(
        new BoundedChannelOptions(NotificationQueueCapacity)
        {
            SingleReader = true,
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.Wait,
        });

    // Bounded queue of inbound requests drained by a fixed pool of workers. Only the read loop writes
    // to it (SingleWriter), the workers read (multiple readers), and a full queue blocks the writer so
    // the peer is throttled instead of letting the host buffer inbound requests without limit.
    private readonly Channel<InboundRequestEnvelope> _inboundRequestQueue = Channel.CreateBounded<InboundRequestEnvelope>(
        new BoundedChannelOptions(InboundRequestQueueCapacity)
        {
            SingleReader = false,
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.Wait,
        });

    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly CancellationTokenSource _disposalCts = new();

    private int _nextRequestId;
    private int _connectionState = StateOpen;
    private long _droppedNotifications;
    private Task? _readLoopTask;
    private Task? _errorPumpTask;
    private Task? _notificationConsumerTask;
    private Task[]? _inboundRequestWorkers;
    private volatile bool _disposed;
    private int _disconnectedRaised;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonRpcConnection"/> class.
    /// </summary>
    /// <param name="input">The stream to read incoming framed messages from (for example, a process's standard output).</param>
    /// <param name="output">The stream to write outgoing framed messages to (for example, a process's standard input).</param>
    /// <param name="errorStream">An optional stream carrying out-of-band diagnostics (for example, a process's standard error). It is logged but is never part of the protocol.</param>
    /// <param name="requestTimeout">The per-request timeout. Defaults to 10 seconds when null.</param>
    /// <param name="writeTimeout">The maximum time a single outbound frame may take to reach the peer before the write is abandoned and the connection is torn down. Defaults to 10 seconds when null.</param>
    public JsonRpcConnection(Stream input, Stream output, Stream? errorStream = null, TimeSpan? requestTimeout = null, TimeSpan? writeTimeout = null)
    {
        _input = input ?? throw new ArgumentNullException(nameof(input));
        _output = output ?? throw new ArgumentNullException(nameof(output));
        _errorStream = errorStream;
        _requestTimeout = requestTimeout ?? TimeSpan.FromSeconds(10);
        _writeTimeout = writeTimeout ?? TimeSpan.FromSeconds(10);
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
        _notificationConsumerTask = Task.Run(ConsumeNotificationsAsync);

        var workers = new Task[InboundRequestWorkerCount];
        for (var i = 0; i < workers.Length; i++)
        {
            workers[i] = Task.Run(ProcessInboundRequestsAsync);
        }

        _inboundRequestWorkers = workers;

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

        // Fail fast once the connection is closed rather than waiting for the request timeout.
        if (Volatile.Read(ref _connectionState) != StateOpen)
        {
            throw new JsonRpcException("The JSON-RPC connection is closed.");
        }

        var id = Interlocked.Increment(ref _nextRequestId);
        var request = new JsonRpcRequest
        {
            Id = id,
            Method = method,
            Params = parameters,
        };

        var tcs = new TaskCompletionSource<JsonRpcResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingRequests[id] = tcs;

        // Close the add/disconnect race: the reader may have exited between the check above and the
        // add. The terminal state is set before FailAllPending runs, so re-reading it here guarantees
        // that either FailAllPending already observed this entry or we observe the closed state and
        // fail immediately, never waiting the full timeout for a response that can never arrive.
        if (Volatile.Read(ref _connectionState) != StateOpen)
        {
            _pendingRequests.TryRemove(id, out _);
            throw new JsonRpcException("The JSON-RPC connection was closed before the request could be sent.");
        }

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

            if (_disposed || _disposalCts.IsCancellationRequested || Volatile.Read(ref _connectionState) != StateOpen)
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

        // (a) Enter the terminal closed state so new writes are rejected before they touch the
        // write lock, (b) cancel the disposal token, fail every pending request, and raise
        // Disconnected exactly once.
        Close("The JSON-RPC connection was disposed.");

        // (c) Complete the notification and inbound-request queues so their consumers drain and exit.
        _notificationQueue.Writer.TryComplete();
        _inboundRequestQueue.Writer.TryComplete();

        // (d) Drain any writer that is mid-frame by acquiring the write lock once. Writes emit their
        // header, body, and flush under CancellationToken.None, so an in-flight write completes the
        // whole frame rather than leaving a corrupt partial frame behind.
        var acquiredWriteLock = false;
        try
        {
            acquiredWriteLock = _writeLock.Wait(TimeSpan.FromSeconds(2));
        }
        catch (ObjectDisposedException)
        {
        }

        WaitForBackgroundTask(_readLoopTask);
        WaitForBackgroundTask(_errorPumpTask);
        WaitForBackgroundTask(_notificationConsumerTask);

        if (_inboundRequestWorkers is { } workers)
        {
            foreach (var worker in workers)
            {
                WaitForBackgroundTask(worker);
            }
        }

        if (acquiredWriteLock)
        {
            try
            {
                _writeLock.Release();
            }
            catch (ObjectDisposedException)
            {
            }
            catch (SemaphoreFullException)
            {
            }
        }

        // (e) Dispose the write lock only after draining, so no in-flight writer can still release it.
        _writeLock.Dispose();
        _disposalCts.Dispose();
    }

    private static void WaitForBackgroundTask(Task? task)
    {
        try
        {
            task?.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
        }
    }

    /// <summary>
    /// Transitions the connection to its terminal closed state exactly once: cancels the disposal
    /// token, fails every pending request, and raises <see cref="Disconnected"/>. Safe to call from
    /// the reader, a failed writer, or Dispose; repeated calls are no-ops.
    /// </summary>
    /// <param name="reason">A human-readable reason recorded on failed pending requests.</param>
    private void Close(string reason)
    {
        // The state flag is flipped before FailAllPending so that a request racing the close either
        // has already been observed by FailAllPending or observes the closed state on its own re-check.
        if (Interlocked.Exchange(ref _connectionState, StateClosed) == StateClosed)
        {
            return;
        }

        try
        {
            _disposalCts.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        _notificationQueue.Writer.TryComplete();
        _inboundRequestQueue.Writer.TryComplete();

        FailAllPending(reason);
        RaiseDisconnected();
    }

    private async Task WriteFramedAsync(string json, CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref _connectionState) != StateOpen)
        {
            throw new JsonRpcException("The JSON-RPC connection is closed.");
        }

        var body = Encoding.UTF8.GetBytes(json);
        var header = Encoding.ASCII.GetBytes($"Content-Length: {body.Length}\r\n\r\n");

        try
        {
            await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
            // The connection was disposed while waiting for the write lock.
            throw new JsonRpcException("The JSON-RPC connection is closed.");
        }

        try
        {
            // Re-check after acquiring the lock: the connection may have closed while we waited.
            if (Volatile.Read(ref _connectionState) != StateOpen)
            {
                throw new JsonRpcException("The JSON-RPC connection is closed.");
            }

            // Once frame emission begins the header, body, and flush must be written as one unit, so
            // the caller's cancellation token is deliberately not honored here: cancelling between the
            // header and body would leave a corrupt partial frame on a still-open connection. Instead
            // the emission is bounded by a dedicated write timeout and by disposal. If the peer stops
            // draining stdin the write cannot block forever: when the timeout or disposal fires the
            // write is abandoned and the connection is torn down (never reused), so a partial frame can
            // only ever appear on a connection that is already closing.
            using var writeCts = CancellationTokenSource.CreateLinkedTokenSource(_disposalCts.Token);
            writeCts.CancelAfter(_writeTimeout);

            try
            {
                await _output.WriteAsync(header, writeCts.Token).ConfigureAwait(false);
                await _output.WriteAsync(body, writeCts.Token).ConfigureAwait(false);
                await _output.FlushAsync(writeCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (writeCts.IsCancellationRequested)
            {
                // The write did not complete within the write timeout, or disposal cancelled it. The
                // peer is no longer draining stdin (or is gone), so the stream can no longer be trusted:
                // enter the terminal closed state, which raises Disconnected so the owner tears the child
                // process down, and fail this write instead of blocking the write lock indefinitely.
                Close("The JSON-RPC connection failed because a write did not complete in time.");
                throw new JsonRpcException("The JSON-RPC connection failed because a write did not complete in time.");
            }
            catch (Exception ex) when (ex is not JsonRpcException)
            {
                // A partial frame may have reached the peer. The stream can no longer be trusted, so
                // the connection transitions to its terminal closed state and is never reused.
                Close("The JSON-RPC connection failed while writing a message.");
                throw new JsonRpcException("The JSON-RPC connection failed while writing a message.", ex);
            }
        }
        finally
        {
            try
            {
                _writeLock.Release();
            }
            catch (ObjectDisposedException)
            {
            }
            catch (SemaphoreFullException)
            {
            }
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
                await DispatchMessageAsync(json).ConfigureAwait(false);
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
            // The reader has exited: EOF, a protocol failure, or disposal. Enter the terminal closed
            // state so pending requests fail and new requests are rejected without waiting.
            Close("The JSON-RPC connection was closed.");
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

    private async Task DispatchMessageAsync(string json)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            // Bound the logged payload: a malformed body can be as large as the frame cap, so only a
            // short prefix is recorded (with a truncation marker) to keep a single bad frame from
            // flooding the log.
            Logger.LogError($"Failed to parse an inbound JSON-RPC message: {TruncateForLog(json)}", ex);
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
                await EnqueueInboundRequestAsync(methodElement.GetString() ?? string.Empty, idElement, root).ConfigureAwait(false);
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

    internal static string TruncateForLog(string value)
    {
        if (value.Length <= MaxLoggedBodyChars)
        {
            return value;
        }

        return $"{value.Substring(0, MaxLoggedBodyChars)}... [truncated; {value.Length} total characters]";
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
        if (!_notificationHandlers.ContainsKey(method))
        {
            Logger.LogDebug($"No handler registered for JSON-RPC notification '{method}'.");
            return;
        }

        var parameters = root.TryGetProperty("params", out var p) ? p.Clone() : default;

        // Enqueue rather than invoke inline so a slow or reentrant handler never blocks the read loop
        // or delays response correlation. The read loop is the only producer for this queue.
        if (_notificationQueue.Writer.TryWrite(new NotificationEnvelope(method, parameters)))
        {
            return;
        }

        // The queue is full because the consumer is behind. Drop the oldest buffered notification to
        // make room for the newest, so the reader stays responsive and the freshest state wins.
        // Notifications are advisory, so losing an older one is preferable to blocking the transport.
        _notificationQueue.Reader.TryRead(out _);

        if (!_notificationQueue.Writer.TryWrite(new NotificationEnvelope(method, parameters)))
        {
            // The queue completed (the connection is closing); nothing further to do.
            return;
        }

        var dropped = Interlocked.Increment(ref _droppedNotifications);
        if ((dropped & 0x3F) == 1)
        {
            Logger.LogWarning($"The JSON-RPC notification queue is saturated; {dropped} notification(s) have been dropped (oldest first).");
        }
    }

    private async Task ConsumeNotificationsAsync()
    {
        try
        {
            while (await _notificationQueue.Reader.WaitToReadAsync(_disposalCts.Token).ConfigureAwait(false))
            {
                while (_notificationQueue.Reader.TryRead(out var envelope))
                {
                    InvokeNotificationHandler(envelope.Method, envelope.Parameters);
                }
            }
        }
        catch (OperationCanceledException) when (_disposalCts.IsCancellationRequested)
        {
        }
        catch (ChannelClosedException)
        {
        }
    }

    private void InvokeNotificationHandler(string method, JsonElement parameters)
    {
        if (!_notificationHandlers.TryGetValue(method, out var handler))
        {
            return;
        }

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

    private async Task EnqueueInboundRequestAsync(string method, JsonElement idElement, JsonElement root)
    {
        // Clone the id and params so the buffered envelope stays valid after the source document is
        // disposed by the read loop.
        var envelope = new InboundRequestEnvelope(
            method,
            idElement.Clone(),
            root.TryGetProperty("params", out var p) ? p.Clone() : default);

        try
        {
            // Hand the request to the bounded worker pool. When the queue is full this awaits, which
            // throttles the read loop (backpressure) instead of spawning unbounded handler tasks. The
            // workers, not the read loop, run the handler so a slow handler cannot stall framing.
            await _inboundRequestQueue.Writer.WriteAsync(envelope, _disposalCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_disposalCts.IsCancellationRequested)
        {
            // The connection is closing; the request is dropped along with everything else in flight.
        }
        catch (ChannelClosedException)
        {
            // The queue was completed because the connection is closing; nothing further to do.
        }
    }

    private async Task ProcessInboundRequestsAsync()
    {
        try
        {
            while (await _inboundRequestQueue.Reader.WaitToReadAsync(_disposalCts.Token).ConfigureAwait(false))
            {
                while (_inboundRequestQueue.Reader.TryRead(out var envelope))
                {
                    await DispatchInboundRequestAsync(envelope).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) when (_disposalCts.IsCancellationRequested)
        {
        }
        catch (ChannelClosedException)
        {
        }
    }

    private Task DispatchInboundRequestAsync(InboundRequestEnvelope envelope)
    {
        if (!_requestHandlers.TryGetValue(envelope.Method, out var handler))
        {
            return SendErrorResponseAsync(envelope.Id, JsonRpcError.MethodNotFound, $"The method '{envelope.Method}' is not supported.");
        }

        return HandleInboundRequestAsync(envelope.Method, envelope.Id, envelope.Parameters, handler);
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
            var reader = new BoundedStderrReader(line => Logger.LogWarning($"[extension stderr] {line}"));
            await reader.PumpAsync(_errorStream!, _disposalCts.Token).ConfigureAwait(false);
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
        if (Interlocked.Exchange(ref _disconnectedRaised, 1) == 0)
        {
            Disconnected?.Invoke(this, EventArgs.Empty);
        }
    }

    private void RaiseError(Exception exception)
    {
        Error?.Invoke(this, new JsonRpcErrorEventArgs(exception));
    }

    /// <summary>
    /// A buffered inbound notification: the method name plus a detached clone of its parameters.
    /// </summary>
    private readonly record struct NotificationEnvelope(string Method, JsonElement Parameters);

    /// <summary>
    /// A buffered inbound request: the method name plus detached clones of its id and parameters, so
    /// the envelope remains valid after the source document is disposed by the read loop.
    /// </summary>
    private readonly record struct InboundRequestEnvelope(string Method, JsonElement Id, JsonElement Parameters);
}
