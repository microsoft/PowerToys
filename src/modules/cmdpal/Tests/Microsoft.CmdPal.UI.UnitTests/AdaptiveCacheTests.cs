// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.UI.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.UnitTests;

[TestClass]
public class AdaptiveCacheTests
{
    [TestMethod]
    public void ApproximateCountChangesOnlyForSuccessfulMutations()
    {
        var cache = new AdaptiveCache<int, int>(capacity: 8);

        cache.Add(1, 1);
        cache.Add(1, 2);
        _ = cache.GetOrAdd(1, static (key, _) => key, 0);

        Assert.AreEqual(1, cache.ApproximateCount);
        Assert.IsFalse(cache.TryRemove(2));
        Assert.AreEqual(1, cache.ApproximateCount);
        Assert.IsTrue(cache.TryRemove(1));
        Assert.AreEqual(0, cache.ApproximateCount);

        _ = cache.GetOrAdd(2, static (key, _) => key, 0);
        Assert.AreEqual(1, cache.ApproximateCount);

        cache.Clear();
        Assert.AreEqual(0, cache.ApproximateCount);
    }

    [TestMethod]
    [Timeout(15_000)]
    public async Task ConcurrentCleanupAndFailedLoadsKeepApproximateCountConsistent()
    {
        const int capacity = 1;
        const int workerCount = 8;
        const int itemsPerWorker = 256;
        var cache = new AdaptiveCache<int, Task<int>>(capacity, TimeSpan.FromHours(1));
        var faultRemovals = new ConcurrentBag<Task>();

        cache.Add(-2, Task.FromResult(-2));
        cache.Add(-1, Task.FromResult(-1));

        Assert.IsTrue(
            SpinWait.SpinUntil(() => cache.ApproximateCount <= capacity, TimeSpan.FromSeconds(5)),
            "Capacity cleanup did not run.");

        var workers = Enumerable.Range(0, workerCount)
            .Select(worker => Task.Run(() =>
            {
                for (var item = 0; item < itemsPerWorker; item++)
                {
                    var key = (worker * itemsPerWorker) + item;

                    if (item % 4 == 0)
                    {
                        var completionSource = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
                        cache.Add(key, completionSource.Task);

                        faultRemovals.Add(completionSource.Task.ContinueWith(
                            completedTask =>
                            {
                                _ = completedTask.Exception;
                                cache.TryRemove(key);
                            },
                            CancellationToken.None,
                            TaskContinuationOptions.OnlyOnFaulted,
                            TaskScheduler.Default));

                        completionSource.SetException(new InvalidOperationException("Icon load failed."));
                    }
                    else
                    {
                        _ = cache.GetOrAdd(key, static (currentKey, _) => Task.FromResult(currentKey), 0);
                    }

                    _ = cache.TryGet(key, out _);
                }
            }))
            .ToArray();

        await Task.WhenAll(workers);
        await Task.WhenAll(faultRemovals);

        cache.Clear();

        Assert.AreEqual(0, cache.ApproximateCount);
        for (var key = 0; key < workerCount * itemsPerWorker; key++)
        {
            Assert.IsFalse(cache.TryGet(key, out _), $"Cache still contains key {key} after Clear.");
        }
    }
}
