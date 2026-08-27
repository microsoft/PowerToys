// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using ManagedCommon;
using StreamJsonRpc;

namespace Microsoft.CmdPal.UI.ViewModels.Services.JsonRpc;

/// <summary>
/// Adapts StreamJsonRpc to the raw JSON contract used by JavaScript extensions.
/// </summary>
public sealed class JsonRpcConnection : IDisposable
{
    private const int NotificationQueueCapacity = 1024;

    private static readonly TimeSpan DefaultRequestTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan DisposeDrainTimeout = TimeSpan.FromSeconds(2);

    private readonly CmdPalJsonRpc _rpc;
    private readonly IJsonRpcMessageHandler _messageHandler;
    private readonly IJsonRpcMessageFactory _messageFactory;
    private readonly Stream? _errorStream;
    private readonly TimeSpan _requestTimeout;
    private readonly CancellationTokenSource _disposalCts = new();
    private readonly CancellationTokenSource _connectionClosedCts = new();
    private readonly ConcurrentDictionary<string, Action<JsonElement>> _notificationHandlers = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, Func<JsonElement, CancellationToken, Task<JsonNode?>>> _requestHandlers = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, RpcMethodTarget> _registeredMethods = new(StringComparer.Ordinal);
    private readonly Channel<NotificationEnvelope> _notificationQueue = Channel.CreateBounded<NotificationEnvelope>(
        new BoundedChannelOptions(NotificationQueueCapacity)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait,
        });

    private Task? _notificationConsumerTask;
    private Task? _errorPumpTask;
    private int _started;
    private int _disposed;
    private int _disconnectedRaised;
    private int _nextRequestId;
    private long _droppedNotifications;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonRpcConnection"/> class.
    /// </summary>
    /// <param name="input">The stream carrying messages from the extension.</param>
    /// <param name="output">The stream carrying messages to the extension.</param>
    /// <param name="errorStream">An optional stream carrying extension diagnostics.</param>
    /// <param name="requestTimeout">The timeout applied to requests.</param>
    public JsonRpcConnection(Stream input, Stream output, Stream? errorStream = null, TimeSpan? requestTimeout = null)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);

        _errorStream = errorStream;
        _requestTimeout = requestTimeout ?? DefaultRequestTimeout;

        var formatter = new SystemTextJsonFormatter
        {
            JsonSerializerOptions =
            {
                TypeInfoResolver = JsonRpcSerializerContext.Default,
            },
        };
        _messageFactory = formatter;

        _messageHandler = new HeaderDelimitedMessageHandler(output, input, formatter);
        _rpc = new CmdPalJsonRpc(_messageHandler)
        {
            AllowModificationWhileListening = true,
            CancelLocallyInvokedMethodsWhenConnectionIsClosed = true,
            SynchronizationContext = null,
        };

        _rpc.Disconnected += OnRpcDisconnected;
    }

    /// <summary>
    /// Raised when the underlying connection closes.
    /// </summary>
    public event EventHandler? Disconnected;

    /// <summary>
    /// Raised when the connection encounters a protocol or handler error.
    /// </summary>
    public event EventHandler<JsonRpcErrorEventArgs>? Error;

    internal long DroppedNotificationCount => Interlocked.Read(ref _droppedNotifications);

    internal Task NotificationConsumerCompletion => _notificationConsumerTask ?? Task.CompletedTask;

    /// <summary>
    /// Starts listening for messages from the extension.
    /// </summary>
    public void StartListening()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);

        if (Interlocked.Exchange(ref _started, 1) != 0)
        {
            throw new InvalidOperationException("The connection is already listening.");
        }

        _notificationConsumerTask = Task.Run(ConsumeNotificationsAsync);
        if (_errorStream is not null)
        {
            _errorPumpTask = Task.Run(PumpErrorStreamAsync);
        }

        _rpc.StartListening();
    }

    /// <summary>
    /// Sends a request and returns its raw result or error.
    /// </summary>
    /// <param name="method">The remote method name.</param>
    /// <param name="parameters">The parameter object.</param>
    /// <param name="cancellationToken">A token that cancels the local wait.</param>
    /// <returns>The request result or error.</returns>
    public async Task<JsonRpcResponse> SendRequestAsync(string method, JsonNode? parameters, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        ArgumentException.ThrowIfNullOrEmpty(method);

        var requestId = Interlocked.Increment(ref _nextRequestId);
        using var requestCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _connectionClosedCts.Token);
        try
        {
            var invokeTask = _rpc.InvokeWithParameterObjectAsync<JsonElement>(requestId, method, parameters, requestCts.Token);
            var result = await invokeTask.WaitAsync(_requestTimeout, cancellationToken).ConfigureAwait(false);

            return new JsonRpcResponse
            {
                Id = requestId,
                Result = result.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ? null : result,
            };
        }
        catch (ConnectionLostException ex)
        {
            throw new JsonRpcException("The JSON-RPC connection closed before a response was received.", ex);
        }
        catch (RemoteRpcException ex)
        {
            return new JsonRpcResponse
            {
                Id = requestId,
                Error = new JsonRpcError
                {
                    Code = GetErrorCode(ex),
                    Message = ex.Message,
                    Data = GetErrorData(ex),
                },
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            requestCts.Cancel();
            throw;
        }
        catch (OperationCanceledException) when (_connectionClosedCts.IsCancellationRequested)
        {
            throw new JsonRpcException("The JSON-RPC connection closed before a response was received.");
        }
        catch (TimeoutException)
        {
            requestCts.Cancel();
            throw;
        }
        catch (Exception ex) when (ex is ObjectDisposedException or IOException)
        {
            throw new JsonRpcException("The JSON-RPC connection closed before a response was received.", ex);
        }
    }

    /// <summary>
    /// Sends a request and deserializes its successful result.
    /// </summary>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="method">The remote method name.</param>
    /// <param name="parameters">The parameter object.</param>
    /// <param name="resultTypeInfo">Source generated metadata for the result.</param>
    /// <param name="cancellationToken">A token that cancels the local wait.</param>
    /// <returns>The deserialized result.</returns>
    public async Task<TResult?> SendRequestAsync<TResult>(string method, JsonNode? parameters, JsonTypeInfo<TResult> resultTypeInfo, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(resultTypeInfo);

        var response = await SendRequestAsync(method, parameters, cancellationToken).ConfigureAwait(false);
        if (response.Error is not null)
        {
            throw new JsonRpcException(response.Error);
        }

        if (response.Result is not { } result)
        {
            return default;
        }

        return result.Deserialize(resultTypeInfo);
    }

    /// <summary>
    /// Sends a notification to the extension.
    /// </summary>
    /// <param name="method">The remote method name.</param>
    /// <param name="parameters">The parameter object.</param>
    /// <param name="cancellationToken">A token that cancels the send before it starts.</param>
    /// <returns>A task that completes when the notification is sent.</returns>
    public async Task SendNotificationAsync(string method, JsonNode? parameters, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        ArgumentException.ThrowIfNullOrEmpty(method);

        try
        {
            var notification = _messageFactory.CreateRequestMessage();
            notification.Method = method;
            notification.Arguments = parameters;
            await _messageHandler.WriteAsync(notification, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is ConnectionLostException or ObjectDisposedException or IOException or TimeoutException)
        {
            DisposeRpc();
            throw new JsonRpcException("The JSON-RPC connection failed while writing a notification.", ex);
        }
    }

    /// <summary>
    /// Registers a handler for an inbound notification.
    /// </summary>
    /// <param name="method">The notification method name.</param>
    /// <param name="handler">The handler to invoke.</param>
    public void RegisterNotificationHandler(string method, Action<JsonElement> handler)
    {
        ArgumentException.ThrowIfNullOrEmpty(method);
        ArgumentNullException.ThrowIfNull(handler);

        _notificationHandlers[method] = handler;
        RegisterMethod(method);
    }

    /// <summary>
    /// Registers a handler for an inbound request.
    /// </summary>
    /// <param name="method">The request method name.</param>
    /// <param name="handler">The handler to invoke.</param>
    public void RegisterRequestHandler(string method, Func<JsonElement, CancellationToken, Task<JsonNode?>> handler)
    {
        ArgumentException.ThrowIfNullOrEmpty(method);
        ArgumentNullException.ThrowIfNull(handler);

        _requestHandlers[method] = handler;
        RegisterMethod(method);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _notificationQueue.Writer.TryComplete();
        _disposalCts.Cancel();
        _connectionClosedCts.Cancel();
        DisposeRpc();

        var tasks = new[]
        {
            _notificationConsumerTask ?? Task.CompletedTask,
            _errorPumpTask ?? Task.CompletedTask,
            _rpc.Completion,
        };
        try
        {
            Task.WhenAll(tasks).Wait(DisposeDrainTimeout);
        }
        catch (AggregateException)
        {
        }

        _ = DisposeTokenSourcesWhenTasksCompleteAsync(tasks, _disposalCts, _connectionClosedCts);
    }

    private static async Task DisposeTokenSourcesWhenTasksCompleteAsync(
        Task[] tasks,
        CancellationTokenSource disposalCts,
        CancellationTokenSource connectionClosedCts)
    {
        await Task.WhenAll(tasks).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        disposalCts.Dispose();
        connectionClosedCts.Dispose();
    }

    private void RegisterMethod(string method)
    {
        _registeredMethods.GetOrAdd(
            method,
            static (name, connection) =>
            {
                var target = new RpcMethodTarget(connection, name);
                connection._rpc.AddLocalRpcMethod(
                    typeof(RpcMethodTarget).GetMethod(nameof(RpcMethodTarget.InvokeAsync))!,
                    target,
                    new JsonRpcMethodAttribute(name)
                    {
                        UseSingleObjectParameterDeserialization = true,
                    });
                return target;
            },
            this);
    }

    private async Task<JsonNode?> DispatchMethodAsync(string method, JsonElement parameters, CancellationToken cancellationToken)
    {
        if (_rpc.IsResponseExpected)
        {
            if (_requestHandlers.TryGetValue(method, out var requestHandler))
            {
                try
                {
                    return await requestHandler(parameters, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Logger.LogError($"The JSON-RPC request handler for '{method}' failed.", ex);
                    throw new LocalRpcException(ex.Message, ex)
                    {
                        ErrorCode = JsonRpcError.InternalError,
                    };
                }
            }

            throw new LocalRpcException($"No request handler is registered for '{method}'.")
            {
                ErrorCode = JsonRpcError.MethodNotFound,
            };
        }

        if (_notificationHandlers.ContainsKey(method))
        {
            EnqueueNotification(method, parameters);
        }

        return null;
    }

    private void EnqueueNotification(string method, JsonElement parameters)
    {
        var envelope = new NotificationEnvelope(method, parameters.Clone());
        while (!_notificationQueue.Writer.TryWrite(envelope))
        {
            if (_notificationQueue.Reader.TryRead(out _))
            {
                Interlocked.Increment(ref _droppedNotifications);
                continue;
            }

            return;
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
                    if (!_notificationHandlers.TryGetValue(envelope.Method, out var handler))
                    {
                        continue;
                    }

                    try
                    {
                        handler(envelope.Parameters);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"The JSON-RPC notification handler for '{envelope.Method}' failed.", ex);
                        RaiseError(ex);
                    }
                }
            }
        }
        catch (OperationCanceledException) when (_disposalCts.IsCancellationRequested)
        {
        }
        catch (ChannelClosedException)
        {
        }
        catch (Exception ex)
        {
            Logger.LogError("The JSON-RPC notification pump ended unexpectedly.", ex);
            RaiseError(ex);
        }
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

    private void OnRpcDisconnected(object? sender, JsonRpcDisconnectedEventArgs e)
    {
        _connectionClosedCts.Cancel();

        if (e.Exception is not null && e.Reason is not DisconnectedReason.LocallyDisposed and not DisconnectedReason.RemotePartyTerminated)
        {
            RaiseError(e.Exception);
        }

        if (Interlocked.Exchange(ref _disconnectedRaised, 1) == 0)
        {
            Disconnected?.Invoke(this, EventArgs.Empty);
        }
    }

    private void DisposeRpc()
    {
        try
        {
            _rpc.Dispose();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private void RaiseError(Exception exception)
    {
        var handlers = Error;
        if (handlers is null)
        {
            return;
        }

        var eventArgs = new JsonRpcErrorEventArgs(exception);
        foreach (EventHandler<JsonRpcErrorEventArgs> handler in handlers.GetInvocationList())
        {
            try
            {
                handler(this, eventArgs);
            }
            catch (Exception ex)
            {
                Logger.LogError("A JSON-RPC error event handler failed.", ex);
            }
        }
    }

    private static int GetErrorCode(RemoteRpcException exception)
    {
        return exception switch
        {
            RemoteInvocationException invocationException => invocationException.ErrorCode,
            _ when exception.ErrorCode is { } code => (int)code,
            _ => JsonRpcError.InternalError,
        };
    }

    private static JsonNode? GetErrorData(RemoteRpcException exception)
    {
        return exception.ErrorData switch
        {
            JsonElement element when element.ValueKind != JsonValueKind.Undefined => JsonNode.Parse(element.GetRawText()),
            JsonNode node => node.DeepClone(),
            _ => null,
        };
    }

    private sealed class RpcMethodTarget
    {
        private readonly JsonRpcConnection _connection;
        private readonly string _method;

        internal RpcMethodTarget(JsonRpcConnection connection, string method)
        {
            _connection = connection;
            _method = method;
        }

        public Task<JsonNode?> InvokeAsync(JsonElement parameters = default, CancellationToken cancellationToken = default)
        {
            return _connection.DispatchMethodAsync(_method, parameters, cancellationToken);
        }
    }

    private sealed class CmdPalJsonRpc : StreamJsonRpc.JsonRpc
    {
        private readonly AsyncLocal<bool?> _isResponseExpected = new();

        internal CmdPalJsonRpc(IJsonRpcMessageHandler messageHandler)
            : base(messageHandler)
        {
        }

        internal bool IsResponseExpected => _isResponseExpected.Value ?? true;

        internal Task<TResult> InvokeWithParameterObjectAsync<TResult>(
            long requestId,
            string method,
            object? parameters,
            CancellationToken cancellationToken)
        {
            return InvokeCoreAsync<TResult>(
                new RequestId(requestId),
                method,
                parameters is null ? null : new object[] { parameters },
                positionalArgumentDeclaredTypes: null,
                namedArgumentDeclaredTypes: null,
                cancellationToken,
                isParameterObject: true);
        }

        protected override async ValueTask<StreamJsonRpc.Protocol.JsonRpcMessage> DispatchRequestAsync(
            StreamJsonRpc.Protocol.JsonRpcRequest request,
            TargetMethod targetMethod,
            CancellationToken cancellationToken)
        {
            var previousValue = _isResponseExpected.Value;
            _isResponseExpected.Value = request.IsResponseExpected;
            try
            {
                return await base.DispatchRequestAsync(request, targetMethod, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _isResponseExpected.Value = previousValue;
            }
        }

        protected override Type? GetErrorDetailsDataType(StreamJsonRpc.Protocol.JsonRpcError error) => typeof(JsonElement);
    }

    private readonly record struct NotificationEnvelope(string Method, JsonElement Parameters);
}
