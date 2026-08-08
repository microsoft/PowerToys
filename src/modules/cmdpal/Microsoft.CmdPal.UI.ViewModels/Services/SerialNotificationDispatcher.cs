// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Channels;
using System.Threading.Tasks;
using ManagedCommon;

namespace Microsoft.CmdPal.UI.ViewModels.Services;

/// <summary>
/// A single ordered dispatch path for provider add/remove notifications. Every emission
/// is enqueued and run by one worker in strict first-in-first-out order, so a consumer
/// can never observe a provider addition before the removal that was enqueued ahead of
/// it, even when the two originate on different threads (a hot-reload swap, a crash
/// restart, an install, and an uninstall can all race).
/// </summary>
/// <remarks>
/// The service raises the paired removal and addition of a hot-reload or crash-restart as
/// a single enqueued action so the pair is never split by another operation's emission.
/// Because a slow consumer handler runs on the worker rather than the caller, an emission
/// cannot deadlock or reorder against the operation that produced it.
/// </remarks>
internal sealed class SerialNotificationDispatcher : IDisposable
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
    /// Enqueues a notification to be raised on the worker after every notification already
    /// enqueued. Dropped silently once the dispatcher has been disposed.
    /// </summary>
    /// <param name="notification">The emission to run in order.</param>
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

        // Stop accepting new notifications and let the worker drain to completion so a
        // removal already queued ahead of an addition is not stranded.
        _queue.Writer.TryComplete();

        try
        {
            _worker.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
            // The worker swallows handler exceptions; nothing actionable here.
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
                        Logger.LogError($"A provider notification handler threw: {ex.Message}");
                    }
                }
            }
        }
        catch (ChannelClosedException)
        {
        }
    }
}
