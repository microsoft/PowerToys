// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;

using ManagedCommon;
using Microsoft.UI.Dispatching;

namespace Microsoft.PowerToys.Settings.UI.Helpers
{
    public static class NativeEventWaiter
    {
        public static IDisposable Register(string eventName, Action callback)
        {
            var dispatcherQueue = DispatcherQueue.GetForCurrentThread()
                ?? throw new InvalidOperationException("Native event registration requires a DispatcherQueue on the calling thread.");
            return Register(eventName, callback, action => dispatcherQueue.TryEnqueue(() => action()));
        }

        // A dispatcher may invoke inline, but must not synchronously wait for another
        // thread to run the callback (the production dispatcher always enqueues).
        internal static IDisposable Register(string eventName, Action callback, Func<Action, bool> tryEnqueue)
        {
            ArgumentException.ThrowIfNullOrEmpty(eventName);
            ArgumentNullException.ThrowIfNull(callback);
            ArgumentNullException.ThrowIfNull(tryEnqueue);
            return new Registration(eventName, callback, tryEnqueue);
        }

        public static void WaitForEventLoop(string eventName, Action callback)
        {
            var dispatcherQueue = DispatcherQueue.GetForCurrentThread();
            var t = new Thread(() =>
            {
                var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, eventName);
                while (true)
                {
                    if (eventHandle.WaitOne())
                    {
                        dispatcherQueue.TryEnqueue(() => callback());
                    }
                }
            });

            t.IsBackground = true;
            t.Start();
        }

        private sealed class Registration : IDisposable
        {
            private readonly object _callbackLock = new();
            private readonly object _handleLock = new();
            private readonly EventWaitHandle _eventHandle;
            private readonly ManualResetEvent _cancel;
            private readonly Thread _thread;
            private Action _callback;
            private Func<Action, bool> _tryEnqueue;
            private int _callbackThreadId;
            private int _callbackDepth;
            private bool _stopped;

            public Registration(string eventName, Action callback, Func<Action, bool> tryEnqueue)
            {
                _callback = callback;
                _tryEnqueue = tryEnqueue;
                _eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, eventName);
                try
                {
                    _cancel = new ManualResetEvent(false);
                    _thread = new Thread(WaitForEvent)
                    {
                        IsBackground = true,
                        Name = $"NativeEventWaiter_{eventName}",
                    };
                    _thread.Start();
                }
                catch
                {
                    _cancel?.Dispose();
                    _eventHandle.Dispose();
                    throw;
                }
            }

            public void Dispose()
            {
                lock (_callbackLock)
                {
                    _callback = null;
                    Interlocked.Exchange(ref _tryEnqueue, null);
                    Monitor.PulseAll(_callbackLock);
                    while (_callbackDepth != 0 && _callbackThreadId != Environment.CurrentManagedThreadId && Thread.CurrentThread != _thread)
                    {
                        Monitor.Wait(_callbackLock);
                    }
                }

                lock (_handleLock)
                {
                    if (!_stopped)
                    {
                        _cancel.Set();
                    }
                }

                // The worker only queues callbacks; it never waits for the UI thread.
                // A dispatcher that disposes the registration itself cannot join its own worker.
                if (Thread.CurrentThread != _thread)
                {
                    _thread.Join();
                }
            }

            private void WaitForEvent()
            {
                try
                {
                    var handles = new WaitHandle[] { _cancel, _eventHandle };
                    while (WaitHandle.WaitAny(handles) == 1)
                    {
                        var tryEnqueue = Volatile.Read(ref _tryEnqueue);
                        if (tryEnqueue == null)
                        {
                            return;
                        }

                        if (!tryEnqueue(InvokeCallback))
                        {
                            Logger.LogWarning("Native event dispatcher is shutting down; stopping registration.");
                            return;
                        }
                    }
                }
                finally
                {
                    Interlocked.Exchange(ref _callback, null);
                    Interlocked.Exchange(ref _tryEnqueue, null);
                    lock (_handleLock)
                    {
                        _eventHandle.Dispose();
                        _cancel.Dispose();
                        _stopped = true;
                    }
                }
            }

            private void InvokeCallback()
            {
                Action callback;
                lock (_callbackLock)
                {
                    while (_callback != null && _callbackDepth != 0 && _callbackThreadId != Environment.CurrentManagedThreadId)
                    {
                        Monitor.Wait(_callbackLock);
                    }

                    callback = _callback;
                    if (callback == null)
                    {
                        return;
                    }

                    _callbackThreadId = Environment.CurrentManagedThreadId;
                    _callbackDepth++;
                }

                try
                {
                    // Never hold the gate across user code: a callback may dispose while
                    // an inline dispatcher on the worker is waiting to enter this gate.
                    callback();
                }
                finally
                {
                    lock (_callbackLock)
                    {
                        _callbackDepth--;
                        Monitor.PulseAll(_callbackLock);
                    }
                }
            }
        }
    }
}
