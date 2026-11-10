// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.Foundation;

namespace Microsoft.CmdPal.Ext.WindowsPalettes;

internal static class WupAsyncExtensions
{
    // Don't use CsWinRT's AsTask(CancellationToken) for palette sources. It calls IAsyncInfo.Cancel()
    // synchronously from the token callback; if the source completes the operation on another thread while
    // that Cancel() call is in flight, the completion handler waits for the callback to finish and the
    // callback waits for Cancel() to return, which deadlocks the thread that cancelled the token.
    // Here, cancellation stops the await immediately and Cancel() is sent from the thread pool.
    internal static async Task<TResult> AsCancellableTask<TResult, TProgress>(
        this IAsyncOperationWithProgress<TResult, TProgress> operation,
        CancellationToken cancellationToken)
    {
        var task = operation.AsTask();
        if (!cancellationToken.CanBeCanceled)
        {
            return await task.ConfigureAwait(false);
        }

        using var registration = cancellationToken.Register(
            static state => _ = Task.Run(() => CancelOperation((IAsyncInfo)state!)),
            operation);
        try
        {
            return await task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            ObserveFault(task);
            throw;
        }
    }

    private static void CancelOperation(IAsyncInfo operation)
    {
        try
        {
            operation.Cancel();
        }
        catch (Exception ex)
        {
            WupDiagnostics.Write($"Cancelling a palette operation failed: 0x{ex.HResult:X8} {ex.Message}");
        }
    }

    private static void ObserveFault(Task task) =>
        _ = task.ContinueWith(
            static t => _ = t.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
}
