// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Buffers.Binary;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Security.Principal;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ManagedCommon;

namespace WorkspacesLauncherUI.IPC
{
    internal sealed class LauncherConnection : IAsyncDisposable
    {
        private const string PipeNamePrefix = "PowerToys.Workspaces.Launcher.";
        private const int MaximumBodyLength = 4 * 1024 * 1024;
        private const int OperationTimeoutMilliseconds = 5000;

        private readonly int _launcherProcessId;
        private readonly NamedPipeClientStream _pipe;
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
        private readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);
        private readonly object _stateLock = new object();
        private readonly TaskCompletionSource _sendsDrained = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        private LauncherProcessIdentity _parent;
        private Task _connectTask = Task.CompletedTask;
        private Task _receiveTask = Task.CompletedTask;
        private Task _parentTask = Task.CompletedTask;
        private Task _disposeTask;
        private int _pendingSends;
        private bool _authenticated;
        private bool _closed;
        private bool _failed;
        private bool _receiving;

        internal LauncherConnection(int launcherProcessId, string pipeName)
        {
            _launcherProcessId = launcherProcessId;
            _pipe = new NamedPipeClientStream(
                ".",
                pipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous,
                TokenImpersonationLevel.Identification);
        }

        internal event Action Closed;

        internal bool IsOpen
        {
            get
            {
                lock (_stateLock)
                {
                    return _authenticated && !_closed;
                }
            }
        }

        internal bool HasFailed
        {
            get
            {
                lock (_stateLock)
                {
                    return _failed;
                }
            }
        }

        internal static bool TryParseArguments(string[] args, out int launcherProcessId, out string pipeName)
        {
            launcherProcessId = 0;
            pipeName = null;
            if (args.Length != 4)
            {
                return false;
            }

            for (var index = 0; index < args.Length; index += 2)
            {
                if (args[index] == "--launcher-pid" && launcherProcessId == 0)
                {
                    if (!int.TryParse(args[index + 1], NumberStyles.None, CultureInfo.InvariantCulture, out launcherProcessId) ||
                        launcherProcessId <= 0 || launcherProcessId == Environment.ProcessId)
                    {
                        return false;
                    }
                }
                else if (args[index] == "--ipc-name" && pipeName == null)
                {
                    pipeName = args[index + 1];
                }
                else
                {
                    return false;
                }
            }

            if (launcherProcessId == 0 || pipeName == null || !pipeName.StartsWith(PipeNamePrefix, StringComparison.Ordinal))
            {
                return false;
            }

            var suffix = pipeName[PipeNamePrefix.Length..];
            return Guid.TryParseExact(suffix, "B", out var id) &&
                string.Equals(suffix, id.ToString("B"), StringComparison.OrdinalIgnoreCase);
        }

        internal Task ConnectAsync()
        {
            _connectTask = Task.Run(ConnectCoreAsync);
            return _connectTask;
        }

        internal void StartReceiving(Action<LauncherMessage> onMessage)
        {
            lock (_stateLock)
            {
                if (_closed || !_authenticated || _receiving)
                {
                    throw new InvalidOperationException("The launcher connection is not ready to receive.");
                }

                _receiving = true;
                _receiveTask = Task.Run(() => ReceiveAsync(onMessage));
            }
        }

        internal Task<bool> SendReadyAsync()
        {
            return SendAsync(new { protocolVersion = 1, type = "ready" });
        }

        internal Task<bool> SendCancelAsync()
        {
            return SendAsync(new { protocolVersion = 1, type = "cancel" });
        }

        internal Task<bool> SendWarningShownAsync(string requestId, CancellationToken cancellationToken)
        {
            return SendAsync(new { protocolVersion = 1, type = "warning-shown", requestId }, cancellationToken);
        }

        internal Task<bool> SendHeartbeatAsync(string requestId, CancellationToken cancellationToken)
        {
            return SendAsync(new { protocolVersion = 1, type = "heartbeat", requestId }, cancellationToken);
        }

        internal Task<bool> SendResponseAsync(string requestId, bool run, CancellationToken cancellationToken)
        {
            return SendAsync(new { protocolVersion = 1, type = "elevation-response", requestId, choice = run ? "run" : "skip" }, cancellationToken);
        }

        internal void Close()
        {
            CloseCore(null, null);
        }

        internal void Fail(string operation, Exception exception)
        {
            CloseCore(operation, exception);
        }

        internal static void LogFailure(string operation, Exception exception)
        {
            // Exception messages can contain received JSON, executable paths, or command-line arguments.
            var nativeError = exception is Win32Exception win32 ? $", Win32 {win32.NativeErrorCode}" : string.Empty;
            Logger.LogError($"{operation} ({exception.GetType().Name}, HRESULT 0x{exception.HResult:X8}{nativeError}).");
        }

        public ValueTask DisposeAsync()
        {
            Close();
            lock (_stateLock)
            {
                _disposeTask ??= DisposeCoreAsync();
                return new ValueTask(_disposeTask);
            }
        }

        private async Task ConnectCoreAsync()
        {
            var operation = "Unable to validate the Workspaces launcher parent before connecting";
            try
            {
                _lifetime.Token.ThrowIfCancellationRequested();
                _parent = LauncherProcessIdentity.Open(_launcherProcessId);
                _parentTask = WatchParentAsync();
                operation = "Unable to connect to the Workspaces launcher pipe";
                await _pipe.ConnectAsync(10000, _lifetime.Token).ConfigureAwait(false);
                _pipe.ReadMode = PipeTransmissionMode.Byte;
                operation = "Unable to authenticate the Workspaces launcher pipe server";
                _parent.AuthenticatePipe(_pipe, _launcherProcessId);
                lock (_stateLock)
                {
                    _lifetime.Token.ThrowIfCancellationRequested();
                    _authenticated = true;
                }
            }
            catch (Exception exception)
            {
                Fail(operation, exception);
            }
        }

        private async Task WatchParentAsync()
        {
            try
            {
                await _parent.WaitForExitAsync(_lifetime.Token).ConfigureAwait(false);
                Task receiveTask;
                lock (_stateLock)
                {
                    receiveTask = _receiveTask;
                }

                // Drain a buffered shutdown frame before classifying parent exit as a failure.
                // A duplicated server handle must not keep the orphaned UI alive indefinitely.
                await receiveTask.WaitAsync(TimeSpan.FromSeconds(1), _lifetime.Token).ConfigureAwait(false);
                Fail("The Workspaces launcher exited without a graceful UI shutdown", new EndOfStreamException());
            }
            catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                Fail("Unable to monitor the Workspaces launcher lifetime", exception);
            }
        }

        private async Task ReceiveAsync(Action<LauncherMessage> onMessage)
        {
            try
            {
                var header = new byte[sizeof(uint)];
                while (!_lifetime.IsCancellationRequested)
                {
                    // Only an idle channel may wait indefinitely; a partially sent frame is bounded.
                    await _pipe.ReadExactlyAsync(header.AsMemory(0, 1), _lifetime.Token).ConfigureAwait(false);
                    using (var headerTimeout = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token))
                    {
                        headerTimeout.CancelAfter(OperationTimeoutMilliseconds);
                        await _pipe.ReadExactlyAsync(header.AsMemory(1), headerTimeout.Token).ConfigureAwait(false);
                    }

                    var length = BinaryPrimitives.ReadUInt32LittleEndian(header);
                    if (length == 0 || length > MaximumBodyLength)
                    {
                        throw new InvalidDataException("Invalid launcher frame length.");
                    }

                    var body = new byte[(int)length];
                    using (var bodyTimeout = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token))
                    {
                        bodyTimeout.CancelAfter(OperationTimeoutMilliseconds);
                        await _pipe.ReadExactlyAsync(body.AsMemory(), bodyTimeout.Token).ConfigureAwait(false);
                    }

                    var message = LauncherMessage.Parse(body, _launcherProcessId);
                    onMessage(message);
                    if (message.Type == "shutdown")
                    {
                        Close();
                        return;
                    }
                }
            }
            catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                Fail("Unable to receive a valid Workspaces launcher message", exception);
            }
        }

        private async Task<bool> SendAsync<T>(T message, CancellationToken cancellationToken = default)
        {
            lock (_stateLock)
            {
                if (_closed || !_authenticated)
                {
                    return false;
                }

                ++_pendingSends;
            }

            var entered = false;
            var writing = false;
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token, cancellationToken);
                timeout.CancelAfter(OperationTimeoutMilliseconds);
                var body = JsonSerializer.SerializeToUtf8Bytes(message);
                if (body.Length == 0 || body.Length > MaximumBodyLength)
                {
                    throw new InvalidDataException("Invalid outgoing launcher frame length.");
                }

                var header = new byte[sizeof(uint)];
                BinaryPrimitives.WriteUInt32LittleEndian(header, (uint)body.Length);
                await _sendLock.WaitAsync(timeout.Token).ConfigureAwait(false);
                entered = true;
                timeout.Token.ThrowIfCancellationRequested();
                writing = true;
                await _pipe.WriteAsync(header.AsMemory(), timeout.Token).ConfigureAwait(false);
                await _pipe.WriteAsync(body.AsMemory(), timeout.Token).ConfigureAwait(false);
                return true;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested && !writing)
            {
                return false;
            }
            catch (Exception exception)
            {
                // A failed/canceled write might have sent a partial frame. Never reuse that stream.
                Fail("Unable to send a Workspaces launcher message within its deadline", exception);
                return false;
            }
            finally
            {
                if (entered)
                {
                    _sendLock.Release();
                }

                lock (_stateLock)
                {
                    --_pendingSends;
                    if (_closed && _pendingSends == 0)
                    {
                        _sendsDrained.TrySetResult();
                    }
                }
            }
        }

        private void CloseCore(string operation, Exception exception)
        {
            lock (_stateLock)
            {
                if (_closed)
                {
                    return;
                }

                _closed = true;
                _failed = exception != null;
                if (_pendingSends == 0)
                {
                    _sendsDrained.TrySetResult();
                }
            }

            if (exception != null)
            {
                LogFailure(operation, exception);
            }

            _lifetime.Cancel();
            _pipe.Dispose();
            Closed?.Invoke();
        }

        private async Task DisposeCoreAsync()
        {
            // Do not dispose tokens, wait handles, or the semaphore while an operation still uses them.
            await _connectTask.ConfigureAwait(false);
            await Task.WhenAll(_receiveTask, _parentTask, _sendsDrained.Task).ConfigureAwait(false);
            _parent?.Dispose();
            _sendLock.Dispose();
            _lifetime.Dispose();
        }
    }
}
