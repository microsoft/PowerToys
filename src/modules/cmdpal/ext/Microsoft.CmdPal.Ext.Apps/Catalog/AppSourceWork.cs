// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.CmdPal.Ext.Apps.Catalog;

/// <summary>Runs synchronous discovery without changing the priority of shared thread-pool workers.</summary>
internal static partial class AppSourceWork
{
    private const int BackgroundBegin = 0x00010000;

    /// <summary>Runs synchronous discovery on the thread pool or on a dedicated background-mode MTA thread.</summary>
    /// <typeparam name="T">The discovery result.</typeparam>
    /// <param name="work">The synchronous discovery operation.</param>
    /// <param name="background">Whether to use a dedicated thread with reduced CPU and I/O priority.</param>
    /// <param name="cancellationToken">Prevents cancelled work from starting; the operation must observe cancellation during execution.</param>
    public static Task<T> Run<T>(Func<T> work, bool background, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(work);
        if (!background)
        {
            return Task.Run(work, cancellationToken);
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<T>(cancellationToken);
        }

        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var worker = new Thread(() =>
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                // This thread belongs to this recovery scan and exits when it finishes.
                // Windows background mode also lowers its memory and I/O scheduling priority.
                _ = SetThreadPriority(GetCurrentThread(), BackgroundBegin);
                completion.SetResult(work());
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                completion.SetCanceled(cancellationToken);
            }
            catch (Exception ex)
            {
                completion.SetException(ex);
            }
        })
        {
            IsBackground = true,
            Name = "CmdPal app recovery",
            Priority = ThreadPriority.BelowNormal,
        };
        worker.SetApartmentState(ApartmentState.MTA);
        worker.Start();
        return completion.Task;
    }

    // GetCurrentThread returns a borrowed pseudo-handle, which must not be closed.
    [LibraryImport("kernel32.dll")]
    private static partial nint GetCurrentThread();

    [LibraryImport("kernel32.dll")]
    private static partial int SetThreadPriority(nint thread, int priority);
}
