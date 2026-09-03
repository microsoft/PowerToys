// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Channels;
using System.Threading.Tasks;
using ManagedCommon;

namespace Microsoft.CmdPal.UI.ViewModels.Services;

/// <summary>
/// Runs provider add/remove notifications one at a time, in the exact order they were
/// enqueued, on a single dedicated background worker.
/// </summary>
/// <remarks>
/// This is a plain FIFO work queue, not a UI concept: <c>DispatcherQueue</c> is thread-affine
/// (bound to whichever thread created it) and doesn't fit here, because notifications must
/// stay in order no matter which thread produced them. A hot-reload swap, a crash restart,
/// an install, and an uninstall can all race on different threads. A single background
/// worker draining one <see cref="Channel{T}"/> gives that ordering guarantee without any
/// UI-thread dependency.
///
/// A caller that needs a removal and addition to be observed as one atomic pair (e.g. a
/// hot-reload swap) enqueues both together as a single action, so nothing else can be
/// interleaved between them.
/// </remarks>
internal sealed partial class SerialNotificationDispatcher : IDisposable
{
    private readonly Channel<Action> _queue = Channel.CreateUnbounded<Action>(
        new UnboundedChannelOptions
        {
            SingleReader = true,
            AllowSynchronousContinuations = false,
        });

    private readonly Task _worker;
    private bool _disposed;

    public SerialNotificationDispatcher()
    {
        _worker = Task.Run(RunAsync);
    }

    /// <summary>
    /// Queues a notification to run on the worker after everything already queued. Silently
    /// dropped once the dispatcher has been disposed.
    /// </summary>
    /// <param name="notification">The action to run, in order, on the background worker.</param>
    public void Enqueue(Action notification)
    {
        ArgumentNullException.ThrowIfNull(notification);

        if (_disposed)
        {
            return;
        }

        _queue.Writer.TryWrite(notification);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // Stop accepting new work and let the worker finish draining what's already queued,
        // so nothing queued before disposal is stranded.
        _queue.Writer.TryComplete();

        try
        {
            _worker.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
            // RunAsync already catches and logs handler exceptions; nothing to do here.
        }
    }

    private async Task RunAsync()
    {
        try
        {
            while (await _queue.Reader.WaitToReadAsync().ConfigureAwait(false))
            {
                while (_queue.Reader.TryRead(out var notification))
                {
                    try
                    {
                        notification();
                    }
                    catch (Exception ex)
                    {
                        // Isolate one handler's failure so it can't stop later notifications
                        // in the queue from running.
                        Logger.LogError("A provider notification handler threw.", ex);
                    }
                }
            }
        }
        catch (ChannelClosedException)
        {
        }
    }
}
