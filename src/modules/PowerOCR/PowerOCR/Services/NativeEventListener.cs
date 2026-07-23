// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using ManagedCommon;
using Microsoft.UI.Dispatching;

namespace PowerOCR.Services;

internal sealed class NativeEventListener : INativeEventListener
{
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly CancellationTokenSource _cancellation = new();
    private readonly List<Task> _listeners = new();

    public NativeEventListener(DispatcherQueue dispatcherQueue)
    {
        _dispatcherQueue = dispatcherQueue;
    }

    public void Register(string eventName, Action callback)
    {
        _listeners.Add(Task.Run(() =>
        {
            using var eventHandle = new EventWaitHandle(
                false,
                EventResetMode.AutoReset,
                eventName);
            WaitHandle[] handles = [_cancellation.Token.WaitHandle, eventHandle];

            while (WaitHandle.WaitAny(handles) == 1)
            {
                if (!_dispatcherQueue.TryEnqueue(() => callback()))
                {
                    Logger.LogError($"Failed to enqueue callback for native event '{eventName}'.");
                    return;
                }
            }
        }));
    }

    public void Dispose()
    {
        _cancellation.Cancel();
        if (!Task.WaitAll(_listeners.ToArray(), TimeSpan.FromSeconds(2)))
        {
            Logger.LogWarning("Timed out while stopping Text Extractor native event listeners.");
        }

        _cancellation.Dispose();
    }
}
