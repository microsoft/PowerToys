// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Concurrent;
using Windows.Win32;

namespace Microsoft.CmdPal.Ext.Run;

/// <summary>
/// Helper class for running functions async on a single STA thread, that's been
/// initialized with OLE.
///
/// Run needs this so that we can dispatch calls to IACListISF on an STA thread
/// that's had OLE initialized. This lets us have one thread we keep around for
/// all requests, rather than creating a new one per-request.
///
/// to use: Pass a function into `RunOnStaAsync` with a cancellation token, and
/// await the result. We'll run the function's result after running it on our
/// STA thread. Cancel that token if you need to bail on a request early and
/// start something else.
/// </summary>
internal sealed class StaHelperService
{
    private static readonly object _staThreadLock = new();
    private static Thread? _staThread;
    private static BlockingCollection<Action>? _staWorkQueue;
    private static bool _staThreadInitialized;

    /// <summary>
    /// Ensures the persistent STA thread is initialized and running.
    /// The thread initializes OLE once and processes work items from a queue.
    /// </summary>
    private static void EnsureStaThreadInitialized()
    {
        if (_staThreadInitialized)
        {
            return;
        }

        lock (_staThreadLock)
        {
            if (_staThreadInitialized)
            {
                return;
            }

            _staWorkQueue = new BlockingCollection<Action>();
            _staThread = new Thread(StaThreadProc)
            {
                IsBackground = true,
                Name = "OLE STA Worker",
            };
            _staThread.SetApartmentState(ApartmentState.STA);
            _staThread.Start();
            _staThreadInitialized = true;
        }
    }

    /// <summary>
    /// The main procedure for the persistent STA thread.
    /// Initializes OLE once and processes work items until the queue is completed.
    /// </summary>
    private static void StaThreadProc()
    {
        var hr = PInvoke.OleInitialize();
        if (hr < 0)
        {
            // OLE initialization failed, but we still need to process work items
            // (they will fail gracefully)
        }

        try
        {
            foreach (var workItem in _staWorkQueue!.GetConsumingEnumerable())
            {
                try
                {
                    workItem();
                }

                // catch (Exception ex) when (ex.IsInterop())
                catch (Exception)
                {
                    // Exceptions are captured by the work item itself
                }
            }
        }
        finally
        {
            PInvoke.OleUninitialize();
        }
    }

    /// <summary>
    /// Runs a function on the persistent STA thread asynchronously with cancellation support.
    /// </summary>
    /// <typeparam name="T">The return type of the function.</typeparam>
    /// <param name="func">The function to execute on the STA thread.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>The result of the function, or default if cancelled.</returns>
    internal static async Task<T?> RunOnStaAsync<T>(Func<T> func, CancellationToken cancellationToken = default)
    {
        EnsureStaThreadInitialized();

        var tcs = new TaskCompletionSource<T?>(TaskCreationOptions.RunContinuationsAsynchronously);

        // Register cancellation to complete the task as cancelled
        using var registration = cancellationToken.Register(() =>
        {
            tcs.TrySetCanceled(cancellationToken);
        });

        // Check if already cancelled before queuing work
        if (cancellationToken.IsCancellationRequested)
        {
            return default;
        }

        var workItem = () =>
        {
            // Check cancellation before doing work
            if (cancellationToken.IsCancellationRequested)
            {
                tcs.TrySetCanceled(cancellationToken);
                return;
            }

            try
            {
                var result = func();

                // Check cancellation after doing work (result may be stale)
                if (cancellationToken.IsCancellationRequested)
                {
                    tcs.TrySetCanceled(cancellationToken);
                }
                else
                {
                    tcs.TrySetResult(result);
                }
            }
            catch (OperationCanceledException)
            {
                tcs.TrySetCanceled(cancellationToken);
            }

            // catch (Exception ex) when (ex.IsInterop())
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        };

        _staWorkQueue!.Add(workItem, cancellationToken);

        try
        {
            return await tcs.Task;
        }
        catch (OperationCanceledException)
        {
            return default;
        }
    }
}
