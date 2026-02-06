// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;

namespace Microsoft.CmdPal.UI.ViewModels;

public static class ParallelHelper
{
    private const long DefaultItemTimeoutMs = 200;
    private static readonly int DefaultMaxWorkers = Math.Max(2, Environment.ProcessorCount / 2);

    public readonly record struct AdaptiveOptions(
        int InitialWorkerCount = 2,
        int MaxWorkerCount = 0,
        TimeSpan ItemTimeout = default,
        CancellationToken CancellationToken = default);

    public static async Task AdaptiveForEachAdaptiveAsync<T>(
        IReadOnlyList<T> items,
        AdaptiveOptions options,
        Func<T, CancellationToken, ValueTask> body)
    {
        if (items.Count == 0)
        {
            return;
        }

        var cancellationToken = options.CancellationToken;
        var maxWorkers = options.MaxWorkerCount > 0
            ? options.MaxWorkerCount
            : DefaultMaxWorkers;

        var itemTimeoutMs = options.ItemTimeout > TimeSpan.Zero
            ? (long)options.ItemTimeout.TotalMilliseconds
            : DefaultItemTimeoutMs;

        var startingWorkers = Math.Min(options.InitialWorkerCount, maxWorkers);

        var nextItemIndex = -1;
        var workers = new Task[maxWorkers];
        var activeWorkerCount = startingWorkers;

        for (var i = 0; i < startingWorkers; i++)
        {
            workers[i] = SpawnWorker();
        }

        while (true)
        {
            var currentActive = Volatile.Read(ref activeWorkerCount);
            var tasksToWait = new List<Task>(currentActive);
            for (var i = 0; i < currentActive; i++)
            {
                var t = Volatile.Read(ref workers[i]);
                if (t is not null)
                {
                    tasksToWait.Add(t);
                }
            }

            if (tasksToWait.Count == 0)
            {
                break;
            }

            await Task.WhenAll(tasksToWait).WaitAsync(cancellationToken).ConfigureAwait(false);

            if (Volatile.Read(ref activeWorkerCount) == currentActive)
            {
                break;
            }
        }

        return;

        Task SpawnWorker() =>
            Task.Run(
                async () =>
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        var i = Interlocked.Increment(ref nextItemIndex);
                        if (i >= items.Count)
                        {
                            break;
                        }

                        var sw = Stopwatch.StartNew();
                        await body(items[i], cancellationToken).ConfigureAwait(false);

                        if (sw.ElapsedMilliseconds > itemTimeoutMs)
                        {
                            var currentCount = Volatile.Read(ref activeWorkerCount);
                            if (currentCount < maxWorkers)
                            {
                                var nextSlot = Interlocked.CompareExchange(ref activeWorkerCount, currentCount + 1, currentCount);
                                if (nextSlot == currentCount)
                                {
                                    Volatile.Write(ref workers[nextSlot], SpawnWorker());
                                }
                            }
                        }
                    }
                },
                cancellationToken);
    }
}
