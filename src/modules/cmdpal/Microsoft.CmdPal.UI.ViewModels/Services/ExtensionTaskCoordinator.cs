// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.CmdPal.UI.ViewModels.Services;

internal static class ExtensionTaskCoordinator
{
    internal static async Task<IReadOnlyList<TResult>> RunConcurrentlyAsync<TInput, TResult>(
        IEnumerable<TInput> inputs,
        Func<TInput, Task<TResult?>> operation,
        Action<TInput, Exception> onError,
        int maxConcurrency,
        CancellationToken cancellationToken)
        where TResult : class
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxConcurrency, 1);
        using var concurrencyGate = new SemaphoreSlim(maxConcurrency, maxConcurrency);
        var tasks = inputs.Select(async input =>
        {
            var entered = false;
            try
            {
                await concurrencyGate.WaitAsync(cancellationToken).ConfigureAwait(false);
                entered = true;
                cancellationToken.ThrowIfCancellationRequested();
                return await operation(input).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return null;
            }
            catch (Exception ex)
            {
                onError(input, ex);
                return null;
            }
            finally
            {
                if (entered)
                {
                    concurrencyGate.Release();
                }
            }
        });

        var results = await Task.WhenAll(tasks).ConfigureAwait(false);

        // Cancellation is not partial success. The current reload may stop here, and the next
        // reload stops every service before rediscovering all providers.
        cancellationToken.ThrowIfCancellationRequested();
        return results.OfType<TResult>().ToArray();
    }

    internal static async Task<TResult> RunWithConcurrencyLimitAsync<TResult>(
        SemaphoreSlim concurrencyGate,
        Func<Task<TResult>> operation,
        CancellationToken cancellationToken)
    {
        await concurrencyGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await operation().ConfigureAwait(false);
        }
        finally
        {
            concurrencyGate.Release();
        }
    }

    internal static async Task RunBlockingConcurrentlyAsync<T>(
        IReadOnlyList<T> inputs,
        Action<T> operation,
        TimeSpan timeout,
        Action<T, Exception> onError,
        Action onTimeout)
    {
        var tasks = inputs.Select(input => Task.Run(() =>
        {
            try
            {
                operation(input);
            }
            catch (Exception ex)
            {
                onError(input, ex);
            }
        }));

        try
        {
            await Task.WhenAll(tasks).WaitAsync(timeout).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            onTimeout();
        }
    }

    internal static async Task ObserveAsync(
        Task task,
        string operation,
        Action<string, Exception> onError,
        CancellationToken cancellationToken)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            onError(operation, ex);
        }
    }

    internal static Task RunInBackgroundAsync(
        Func<Task> operation,
        string description,
        Action<string, Exception> onError,
        CancellationToken cancellationToken)
    {
        return ObserveAsync(
            Task.Run(operation, cancellationToken),
            description,
            onError,
            cancellationToken);
    }
}
