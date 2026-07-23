// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
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

    // Upper bound on the number of inbound requests buffered ahead of the workers. This is a
    // secondary, count-based guard layered on top of the aggregate byte budget below: the reader
    // admits requests without ever blocking, so a full queue causes a request to be rejected rather
    // than stalling the reader.
    private const int InboundRequestQueueCapacity = 256;

    // Aggregate byte budgets for buffered inbound work. Because a single frame body can be as large
    // as MaxMessageBytes (32 MiB), a purely count-based bound could retain tens of gigabytes of
    // buffered payloads. These caps bound the total bytes held by the notification queue and the
    // inbound request queue independently, regardless of item count.
    internal const long DefaultMaxQueuedNotificationBytes = 64L * 1024 * 1024;
    internal const long DefaultMaxQueuedRequestBytes = 64L * 1024 * 1024;

    // Upper bound on the number of concurrent server-busy rejection responses in flight. The reader
    // sends these without blocking; this cap keeps a sustained overload from spawning unbounded
    // rejection tasks. When exceeded, the request is dropped and the peer observes a timeout.
    private const int MaxConcurrentRejectionSends = 64;

    // Protocol-error logging is rate limited so a peer that streams malformed or undecodable frames
    // cannot flood the log. At most this many protocol-error entries are logged per window.
    private const int ProtocolErrorLogMaxPerWindow = 10;

    // Upper bound on how many characters of an offending payload are written to the log. Malformed
    // or oversized bodies are truncated so a single bad frame cannot flood the log up to the frame cap.
    internal const int MaxLoggedBodyChars = 1024;

    private static readonly TimeSpan ProtocolErrorLogWindow = TimeSpan.FromSeconds(5);

    // A single aggregate budget for draining every background task during disposal. The total drain
    // time is bounded by this one deadline instead of applying a separate per-task timeout, so a
    // connection with many workers still shuts down promptly.
    private static readonly TimeSpan DisposeDrainBudget = TimeSpan.FromSeconds(2);

    private readonly Stream _input;
    private readonly Stream _output;
    private readonly Stream? _errorStream;
    private readonly TimeSpan _requestTimeout;
    private readonly TimeSpan _writeTimeout;
    private readonly long _maxQueuedNotificationBytes;
    private readonly long _maxQueuedRequestBytes;

    private readonly ConcurrentDictionary<int, TaskCompletionSource<JsonRpcResponse>> _pendingRequests = new();
    private readonly ConcurrentDictionary<string, Action<JsonElement>> _notificationHandlers = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, Func<JsonElement, CancellationToken, Task<JsonNode?>>> _requestHandlers = new(StringComparer.Ordinal);

    // The reader (and the drop-oldest path) and the consumer both read this queue, so it is not a
    // single-reader channel. Only the reader writes to it.
    private readonly Channel<NotificationEnvelope> _notificationQueue = Channel.CreateBounded<NotificationEnvelope>(
        new BoundedChannelOptions(NotificationQueueCapacity)
        {
            SingleReader = false,
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.Wait,
        });

    // Bounded queue of inbound requests drained by a fixed pool of workers. Only the read loop writes
    // to it (SingleWriter) and the workers read (multiple readers). The reader admits requests with a
    // non-blocking TryWrite: when the count or byte budget is exhausted the request is rejected with a
    // server-busy error instead of blocking the reader, which must stay free to route responses.
    private readonly Channel<InboundRequestEnvelope> _inboundRequestQueue = Channel.CreateBounded<InboundRequestEnvelope>(
        new BoundedChannelOptions(InboundRequestQueueCapacity)
        {
            SingleReader = false,
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.Wait,
        });

    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly CancellationTokenSource _disposalCts = new();

    // A snapshot of the disposal token taken at construction. Background tasks and the write path use
    // this captured value instead of _disposalCts.Token so they never touch the CancellationTokenSource
    // after it is disposed (which would throw ObjectDisposedException). The token is cancelled before
    // the source is disposed, so the snapshot still reports cancellation correctly.
    private readonly CancellationToken _shutdownToken;

    private readonly RateLimitedProtocolLog _protocolErrorLog;

    private int _nextRequestId;
    private int _connectionState = StateOpen;
    private long _droppedNotifications;
    private long _queuedNotificationBytes;
    private long _queuedRequestBytes;
    private int _pendingRejectionSends;
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
        : this(input, output, errorStream, requestTimeout, writeTimeout, maxQueuedNotificationBytes: null, maxQueuedRequestBytes: null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonRpcConnection"/> class with explicit aggregate
    /// byte budgets for buffered inbound work. Used by tests to exercise the byte-budget rejection path.
    /// </summary>
    /// <param name="input">The stream to read incoming framed messages from.</param>
    /// <param name="output">The stream to write outgoing framed messages to.</param>
    /// <param name="errorStream">An optional stream carrying out-of-band diagnostics.</param>
    /// <param name="requestTimeout">The per-request timeout. Defaults to 10 seconds when null.</param>
    /// <param name="writeTimeout">The per-write timeout. Defaults to 10 seconds when null.</param>
    /// <param name="maxQueuedNotificationBytes">The aggregate byte budget for buffered notifications. Defaults to <see cref="DefaultMaxQueuedNotificationBytes"/> when null.</param>
    /// <param name="maxQueuedRequestBytes">The aggregate byte budget for buffered inbound requests. Defaults to <see cref="DefaultMaxQueuedRequestBytes"/> when null.</param>
    internal JsonRpcConnection(Stream input, Stream output, Stream? errorStream, TimeSpan? requestTimeout, TimeSpan? writeTimeout, long? maxQueuedNotificationBytes, long? maxQueuedRequestBytes)
    {
        _input = input ?? throw new ArgumentNullException(nameof(input));
        _output = output ?? throw new ArgumentNullException(nameof(output));
        _errorStream = errorStream;
        _requestTimeout = requestTimeout ?? TimeSpan.FromSeconds(10);
        _writeTimeout = writeTimeout ?? TimeSpan.FromSeconds(10);
        _maxQueuedNotificationBytes = Math.Max(1, maxQueuedNotificationBytes ?? DefaultMaxQueuedNotificationBytes);
        _maxQueuedRequestBytes = Math.Max(1, maxQueuedRequestBytes ?? DefaultMaxQueuedRequestBytes);
        _shutdownToken = _disposalCts.Token;
        _protocolErrorLog = new RateLimitedProtocolLog(
            ProtocolErrorLogMaxPerWindow,
            ProtocolErrorLogWindow,
            static suppressed => Logger.LogWarning($"Suppressed {suppressed} JSON-RPC protocol-error log entries in the previous window to avoid flooding the log."));
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
    /// Gets the number of inbound notifications that have been dropped because the notification queue
    /// exceeded its count or byte budget. Exposed for tests.
    /// </summary>
    internal long DroppedNotificationCount => Interlocked.Read(ref _droppedNotifications);

    /// <summary>
    /// Gets the total number of protocol-error log entries suppressed by the rate limiter. Exposed for tests.
    /// </summary>
    internal long SuppressedProtocolErrorLogCount => _protocolErrorLog.TotalSuppressed;

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

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _shutdownToken);
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

            if (_disposed || _shutdownToken.IsCancellationRequested || Volatile.Read(ref _connectionState) != StateOpen)
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
    /// Removes the notification handler registered for a method, if any. Safe to
    /// call when no handler is registered.
    /// </summary>
    /// <param name="method">The notification method name.</param>
    public void UnregisterNotificationHandler(string method)
    {
        if (string.IsNullOrEmpty(method))
        {
            return;
        }

        _notificationHandlers.TryRemove(method, out _);
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

        // (d) Drain the write lock and every background task under a single aggregate deadline so the
        // total shutdown time is bounded by one budget, not by a separate timeout per task. Acquiring
        // the write lock once lets any writer that is mid-frame finish (or be abandoned by disposal).
        var drainStopwatch = Stopwatch.StartNew();

        var acquiredWriteLock = false;
        try
        {
            acquiredWriteLock = _writeLock.Wait(RemainingDrainBudget(drainStopwatch));
        }
        catch (ObjectDisposedException)
        {
        }

        var backgroundTasks = new List<Task>(InboundRequestWorkerCount + 3);
        AddIfNotNull(backgroundTasks, _readLoopTask);
        AddIfNotNull(backgroundTasks, _errorPumpTask);
        AddIfNotNull(backgroundTasks, _notificationConsumerTask);
        if (_inboundRequestWorkers is { } workers)
        {
            backgroundTasks.AddRange(workers);
        }

        if (backgroundTasks.Count > 0)
        {
            try
            {
                Task.WaitAll(backgroundTasks.ToArray(), RemainingDrainBudget(drainStopwatch));
            }
            catch (AggregateException)
            {
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

    private static void AddIfNotNull(List<Task> tasks, Task? task)
    {
        if (task is not null)
        {
            tasks.Add(task);
        }
    }

    private static TimeSpan RemainingDrainBudget(Stopwatch stopwatch)
    {
        var remaining = DisposeDrainBudget - stopwatch.Elapsed;
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
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
            using var writeCts = CancellationTokenSource.CreateLinkedTokenSource(_shutdownToken);
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
            while (!_shutdownToken.IsCancellationRequested)
            {
                var contentLength = await ReadHeaderAsync(_shutdownToken).ConfigureAwait(false);
                if (contentLength < 0)
                {
                    break;
                }

                if (contentLength == 0)
                {
                    continue;
                }

                var body = await ReadExactAsync(contentLength, _shutdownToken).ConfigureAwait(false);
                if (body is null)
                {
                    break;
                }

                var json = Encoding.UTF8.GetString(body);

                // Dispatch synchronously so the reader never awaits a bounded resource. Response frames
                // are routed immediately and inbound requests/notifications are admitted without blocking,
                // which keeps response correlation alive even when the inbound work queues are saturated.
                DispatchMessage(json, body.Length);
            }
        }
        catch (OperationCanceledException) when (_shutdownToken.IsCancellationRequested)
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

    private void DispatchMessage(string json, int frameByteLength)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            // Bound the logged payload: a malformed body can be as large as the frame cap, so only a
            // short prefix is recorded (with a truncation marker). Route it through the rate limiter so
            // a flood of malformed frames cannot flood the log.
            _protocolErrorLog.Run(() => Logger.LogError($"Failed to parse an inbound JSON-RPC message: {TruncateForLog(json)}", ex));
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
                EnqueueNotification(methodElement.GetString() ?? string.Empty, root, frameByteLength);
            }
            else if (hasMethod && hasId)
            {
                EnqueueInboundRequest(methodElement.GetString() ?? string.Empty, idElement, root, frameByteLength);
            }
            else if (hasId)
            {
                DispatchResponse(idElement, json);
            }
            else
            {
                _protocolErrorLog.Run(static () => Logger.LogWarning("Received a JSON-RPC message with neither a method nor an id."));
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
            _protocolErrorLog.Run(static () => Logger.LogWarning("Received a JSON-RPC response with a non-integer id."));
            return;
        }

        JsonRpcResponse? response;
        try
        {
            response = JsonSerializer.Deserialize(json, JsonRpcSerializerContext.Default.JsonRpcResponse);
        }
        catch (JsonException ex)
        {
            _protocolErrorLog.Run(() => Logger.LogError("Failed to deserialize a JSON-RPC response.", ex));
            RaiseError(ex);
            return;
        }

        if (response is null)
        {
            return;
        }

        // Response correlation runs directly on the reader with no bounded-capacity await, so an
        // in-flight handler awaiting this response always completes even when the inbound work queues
        // are saturated.
        if (_pendingRequests.TryRemove(id, out var tcs))
        {
            tcs.TrySetResult(response);
        }
        else
        {
            _protocolErrorLog.Run(() => Logger.LogWarning($"Received a JSON-RPC response for an unknown request id {id}."));
        }
    }

    private void EnqueueNotification(string method, JsonElement root, int sizeBytes)
    {
        if (!_notificationHandlers.ContainsKey(method))
        {
            Logger.LogDebug($"No handler registered for JSON-RPC notification '{method}'.");
            return;
        }

        var parameters = root.TryGetProperty("params", out var p) ? p.Clone() : default;
        var envelope = new NotificationEnvelope(method, parameters, sizeBytes);

        // Enqueue rather than invoke inline so a slow or reentrant handler never blocks the read loop
        // or delays response correlation. The reader never blocks on admission: it makes room within
        // both the count and aggregate byte budgets by dropping the oldest notifications, and if the
        // newest still does not fit it is dropped too. Notifications are advisory, so this is safe.
        while (true)
        {
            var projected = Interlocked.Read(ref _queuedNotificationBytes) + sizeBytes;
            if (projected <= _maxQueuedNotificationBytes && _notificationQueue.Writer.TryWrite(envelope))
            {
                Interlocked.Add(ref _queuedNotificationBytes, sizeBytes);
                return;
            }

            // The byte budget would be exceeded or the count-bounded queue is full. Drop the oldest
            // buffered notification to free space, then retry.
            if (_notificationQueue.Reader.TryRead(out var oldest))
            {
                Interlocked.Add(ref _queuedNotificationBytes, -oldest.SizeBytes);
                RecordDroppedNotification();
                continue;
            }

            // The queue is empty yet the newest notification still does not fit (it alone exceeds the
            // byte budget), or the queue has completed because the connection is closing. Drop it.
            RecordDroppedNotification();
            return;
        }
    }

    private void RecordDroppedNotification()
    {
        var dropped = Interlocked.Increment(ref _droppedNotifications);
        if ((dropped & 0x3F) == 1)
        {
            _protocolErrorLog.Run(() => Logger.LogWarning($"The JSON-RPC notification queue is saturated; {dropped} notification(s) have been dropped."));
        }
    }

    private async Task ConsumeNotificationsAsync()
    {
        try
        {
            while (await _notificationQueue.Reader.WaitToReadAsync(_shutdownToken).ConfigureAwait(false))
            {
                while (_notificationQueue.Reader.TryRead(out var envelope))
                {
                    Interlocked.Add(ref _queuedNotificationBytes, -envelope.SizeBytes);
                    InvokeNotificationHandler(envelope.Method, envelope.Parameters);
                }
            }
        }
        catch (OperationCanceledException) when (_shutdownToken.IsCancellationRequested)
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

    private void EnqueueInboundRequest(string method, JsonElement idElement, JsonElement root, int sizeBytes)
    {
        // Admit the request without ever blocking the reader. Enforce the aggregate byte budget first,
        // then the count-bounded queue. When either budget is exhausted the request is rejected with a
        // server-busy error rather than stalling the reader, which must stay free to route responses.
        var projected = Interlocked.Add(ref _queuedRequestBytes, sizeBytes);
        if (projected > _maxQueuedRequestBytes)
        {
            Interlocked.Add(ref _queuedRequestBytes, -sizeBytes);
            RejectInboundRequest(idElement);
            return;
        }

        // Clone the id and params so the buffered envelope stays valid after the source document is
        // disposed by the read loop.
        var envelope = new InboundRequestEnvelope(
            method,
            idElement.Clone(),
            root.TryGetProperty("params", out var p) ? p.Clone() : default,
            sizeBytes);

        if (_inboundRequestQueue.Writer.TryWrite(envelope))
        {
            return;
        }

        // The count-bounded queue is full (or completed because the connection is closing). Release the
        // byte reservation and reject the request.
        Interlocked.Add(ref _queuedRequestBytes, -sizeBytes);
        RejectInboundRequest(idElement);
    }

    private void RejectInboundRequest(JsonElement idElement)
    {
        if (Volatile.Read(ref _connectionState) != StateOpen)
        {
            // The connection is closing; the peer will observe the disconnect. Nothing to send.
            return;
        }

        // Bound the number of concurrent rejection sends so a sustained overload cannot spawn unbounded
        // tasks. When the cap is reached the rejection is dropped and the peer observes a timeout.
        if (Interlocked.Increment(ref _pendingRejectionSends) > MaxConcurrentRejectionSends)
        {
            Interlocked.Decrement(ref _pendingRejectionSends);
            return;
        }

        var id = idElement.Clone();
        _ = Task.Run(async () =>
        {
            try
            {
                await SendErrorResponseAsync(id, JsonRpcError.ServerBusy, "The server is busy and cannot accept the request.").ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.LogDebug($"Failed to send a JSON-RPC server-busy response: {ex.Message}");
            }
            finally
            {
                Interlocked.Decrement(ref _pendingRejectionSends);
            }
        });
    }

    private async Task ProcessInboundRequestsAsync()
    {
        try
        {
            while (await _inboundRequestQueue.Reader.WaitToReadAsync(_shutdownToken).ConfigureAwait(false))
            {
                while (_inboundRequestQueue.Reader.TryRead(out var envelope))
                {
                    Interlocked.Add(ref _queuedRequestBytes, -envelope.SizeBytes);
                    await DispatchInboundRequestAsync(envelope).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) when (_shutdownToken.IsCancellationRequested)
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
            var result = await handler(parameters, _shutdownToken).ConfigureAwait(false);
            await SendResultResponseAsync(id, result).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_shutdownToken.IsCancellationRequested)
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

        return WriteFramedAsync(response.ToJsonString(), _shutdownToken);
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

        return WriteFramedAsync(response.ToJsonString(), _shutdownToken);
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
            await reader.PumpAsync(_errorStream!, _shutdownToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_shutdownToken.IsCancellationRequested)
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
    /// A buffered inbound notification: the method name, a detached clone of its parameters, and the
    /// byte size charged against the notification queue's aggregate byte budget.
    /// </summary>
    private readonly record struct NotificationEnvelope(string Method, JsonElement Parameters, int SizeBytes);

    /// <summary>
    /// A buffered inbound request: the method name plus detached clones of its id and parameters, so
    /// the envelope remains valid after the source document is disposed by the read loop, and the byte
    /// size charged against the request queue's aggregate byte budget.
    /// </summary>
    private readonly record struct InboundRequestEnvelope(string Method, JsonElement Id, JsonElement Parameters, int SizeBytes);
}
