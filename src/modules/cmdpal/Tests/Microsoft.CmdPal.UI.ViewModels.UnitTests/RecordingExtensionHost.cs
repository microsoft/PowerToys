// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

/// <summary>
/// An <see cref="IExtensionHost"/> that records every status and log call in arrival
/// order and lets a test await a specific number of calls without polling. Used by the
/// proxy startup-notification and status-lifecycle tests.
/// </summary>
internal sealed partial class RecordingExtensionHost : IExtensionHost
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);

    private readonly Lock _gate = new();
    private readonly List<IStatusMessage> _shown = [];
    private readonly List<IStatusMessage> _hidden = [];
    private readonly List<ILogMessage> _logs = [];
    private readonly List<(int Target, TaskCompletionSource Signal)> _shownWaiters = [];
    private readonly List<(int Target, TaskCompletionSource Signal)> _hiddenWaiters = [];
    private readonly List<(int Target, TaskCompletionSource Signal)> _logWaiters = [];

    public IReadOnlyList<IStatusMessage> Shown
    {
        get
        {
            lock (_gate)
            {
                return [.. _shown];
            }
        }
    }

    public IReadOnlyList<IStatusMessage> Hidden
    {
        get
        {
            lock (_gate)
            {
                return [.. _hidden];
            }
        }
    }

    public IReadOnlyList<ILogMessage> Logs
    {
        get
        {
            lock (_gate)
            {
                return [.. _logs];
            }
        }
    }

    public Task WaitForShownCountAsync(int count) => WaitFor(_shown, _shownWaiters, count);

    public Task WaitForHiddenCountAsync(int count) => WaitFor(_hidden, _hiddenWaiters, count);

    public Task WaitForLogCountAsync(int count) => WaitFor(_logs, _logWaiters, count);

    public IAsyncAction ShowStatus(IStatusMessage? message, StatusContext context)
    {
        if (message is not null)
        {
            Record(_shown, _shownWaiters, message);
        }

        return Task.CompletedTask.AsAsyncAction();
    }

    public IAsyncAction HideStatus(IStatusMessage? message)
    {
        if (message is not null)
        {
            Record(_hidden, _hiddenWaiters, message);
        }

        return Task.CompletedTask.AsAsyncAction();
    }

    public IAsyncAction LogMessage(ILogMessage? message)
    {
        if (message is not null)
        {
            Record(_logs, _logWaiters, message);
        }

        return Task.CompletedTask.AsAsyncAction();
    }

    private void Record<T>(List<T> sink, List<(int Target, TaskCompletionSource Signal)> waiters, T value)
    {
        List<TaskCompletionSource> ready = [];
        lock (_gate)
        {
            sink.Add(value);
            var current = sink.Count;
            for (var i = waiters.Count - 1; i >= 0; i--)
            {
                if (current >= waiters[i].Target)
                {
                    ready.Add(waiters[i].Signal);
                    waiters.RemoveAt(i);
                }
            }
        }

        foreach (var signal in ready)
        {
            signal.TrySetResult();
        }
    }

    private Task WaitFor<T>(List<T> sink, List<(int Target, TaskCompletionSource Signal)> waiters, int count)
    {
        TaskCompletionSource signal;
        lock (_gate)
        {
            if (sink.Count >= count)
            {
                return Task.CompletedTask;
            }

            signal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            waiters.Add((count, signal));
        }

        return signal.Task.WaitAsync(DefaultTimeout);
    }
}
