// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Windows.System;
using WinRT;

namespace Microsoft.CmdPal.Ext.ClipboardHistory.Helpers;

internal sealed partial class ClipboardHistoryWorker : IClipboardHistoryWorker
{
    private readonly Lock _gate = new();
    private readonly TaskCompletionSource _drained = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private DispatcherQueueController? _controller;
    private int _pending;
    private Task? _shutdown;

    private static DispatcherQueueController CreateController()
    {
        // Clipboard awaits need both an STA apartment and a running message pump.
        var options = new DispatcherQueueOptions
        {
            Size = Marshal.SizeOf<DispatcherQueueOptions>(),
            ThreadType = DispatcherQueueThreadType.Dedicated,
            ApartmentType = DispatcherQueueApartmentType.Sta,
        };
        Marshal.ThrowExceptionForHR(CreateDispatcherQueueController(options, out var controller));
        try
        {
            return MarshalInterface<DispatcherQueueController>.FromAbi(controller);
        }
        finally
        {
            Marshal.Release(controller);
        }
    }

    public Task RunAsync(Func<Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        DispatcherQueue queue;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_shutdown is not null, this);
            _controller ??= CreateController();
            queue = _controller.DispatcherQueue;
            _pending++;
        }

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            if (!queue.TryEnqueue(async () =>
            {
                SynchronizationContext.SetSynchronizationContext(new QueueSynchronizationContext(queue));
                try
                {
                    await action();
                    completion.SetResult();
                }
                catch (Exception ex)
                {
                    completion.SetException(ex);
                }
                finally
                {
                    CompleteWork();
                }
            }))
            {
                throw new InvalidOperationException("The clipboard dispatcher rejected work.");
            }
        }
        catch (Exception ex)
        {
            completion.SetException(ex);
            CompleteWork();
        }

        return completion.Task;
    }

    public ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            if (_pending == 0)
            {
                _drained.TrySetResult();
            }

            _shutdown ??= ShutdownAsync();
            return new ValueTask(_shutdown);
        }
    }

    private void CompleteWork()
    {
        lock (_gate)
        {
            _pending--;
            if (_pending == 0 && _shutdown is not null)
            {
                _drained.TrySetResult();
            }
        }
    }

    private async Task ShutdownAsync()
    {
        await _drained.Task.ConfigureAwait(false);
        if (_controller is not null)
        {
            await _controller.ShutdownQueueAsync().AsTask().ConfigureAwait(false);
        }
    }

    [DllImport("CoreMessaging.dll")]
    private static extern int CreateDispatcherQueueController(DispatcherQueueOptions options, out IntPtr controller);

    private enum DispatcherQueueThreadType
    {
        Dedicated = 1,
    }

    private enum DispatcherQueueApartmentType
    {
        Sta = 2,
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DispatcherQueueOptions
    {
        public int Size;
        public DispatcherQueueThreadType ThreadType;
        public DispatcherQueueApartmentType ApartmentType;
    }

    private sealed class QueueSynchronizationContext(DispatcherQueue queue) : SynchronizationContext
    {
        public override SynchronizationContext CreateCopy() => new QueueSynchronizationContext(queue);

        public override void Post(SendOrPostCallback callback, object? state)
        {
            if (!queue.TryEnqueue(() =>
            {
                SetSynchronizationContext(this);
                callback(state);
            }))
            {
                throw new InvalidOperationException("The clipboard dispatcher rejected a continuation.");
            }
        }
    }
}
