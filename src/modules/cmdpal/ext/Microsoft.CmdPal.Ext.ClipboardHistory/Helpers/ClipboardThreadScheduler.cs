// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Threading;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.ClipboardHistory.Helpers;

public partial class ClipboardThreadQueue : IDisposable
{
    private readonly Thread _thread;
    private readonly ConcurrentQueue<Action> _taskQueue = new ConcurrentQueue<Action>();
    private readonly AutoResetEvent _taskAvailable = new AutoResetEvent(false);
    private readonly CancellationTokenSource cancellationToken = new CancellationTokenSource();

    public ClipboardThreadQueue()
    {
        _thread = new Thread(() =>
        {
            var hr = NativeMethods.CoInitialize(IntPtr.Zero);
            if (hr != 0)
            {
                ExtensionHost.LogMessage($"CoInitialize failed with HRESULT: {hr}");
            }

            while (true)
            {
                _taskAvailable.WaitOne();

                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                while (_taskQueue.TryDequeue(out var task))
                {
                    try
                    {
                        task();
                    }
                    catch (Exception ex)
                    {
                        ExtensionHost.LogMessage($"Error executing task in ClipboardThreadQueue: {ex.Message}");
                    }
                }
            }

            NativeMethods.CoUninitialize();
        });

        _thread.SetApartmentState(ApartmentState.STA);
        _thread.IsBackground = true;
        _thread.Start();
    }

    public void EnqueueTask(Action task)
    {
        _taskQueue.Enqueue(task);
        _taskAvailable.Set();
    }

    public void Dispose()
    {
        cancellationToken.Cancel();
        _taskAvailable.Set();
        _thread.Join(); // Wait for the thread to finish processing tasks

        _taskAvailable.Dispose();
        GC.SuppressFinalize(this);
    }
}
