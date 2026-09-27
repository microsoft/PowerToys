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
    [Timeout(5_000)]
    public async Task LookupOverlappingEvictionDoesNotObserveAnotherKeysValue()
    {
        using var comparisonStarted = new ManualResetEventSlim();
        using var continueComparison = new ManualResetEventSlim();
        var cache = new AdaptiveCache<BlockingKey, int>(capacity: 4, decayInterval: TimeSpan.FromHours(1));
        cache.Add(new BlockingKey(1), 101);

        var lookup = Task.Run(() =>
        {
            var found = cache.TryGet(
                new BlockingKey(1, comparisonStarted, continueComparison),
                out var value);
            return (Found: found, Value: value);
        });

        Assert.IsTrue(comparisonStarted.Wait(TimeSpan.FromSeconds(2)), "The lookup did not reach its key comparison.");
        try
        {
            Assert.IsTrue(cache.TryRemove(new BlockingKey(1)));
            cache.Add(new BlockingKey(2), 202);
        }
        finally
        {
            continueComparison.Set();
        }

        var result = await lookup;
        Assert.IsTrue(result.Found);
        Assert.AreEqual(101, result.Value);
    }

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
    public void ReplacingValuePreservesAdaptiveFrequency()
    {
        var cache = new AdaptiveCache<int, int>(capacity: 4, decayInterval: TimeSpan.FromHours(1));
        cache.Add(1, 101);
        for (var i = 0; i < 1_000; i++)
        {
            Assert.IsTrue(cache.TryGet(1, out _));
        }

        cache.Add(1, 102);
        cache.Add(2, 202);
        for (var i = 0; i < 2_000; i++)
        {
            Assert.IsTrue(cache.TryGet(2, out _));
        }

        var cleanup = typeof(AdaptiveCache<int, int>).GetMethod(
            "PerformCleanup",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.IsNotNull(cleanup);
        cleanup.Invoke(cache, null);

        Assert.IsTrue(cache.TryGet(1, out var replacement));
        Assert.AreEqual(102, replacement);
    }

    [TestMethod]
    [Timeout(5_000)]
    public async Task GetOrAddRetriesWhenCompetingEntryDisappears()
    {
        using var factoryStarted = new ManualResetEventSlim();
        using var continueFactory = new ManualResetEventSlim();
        using var lookupStarted = new ManualResetEventSlim();
        using var continueLookup = new ManualResetEventSlim();
        var cache = new AdaptiveCache<BlockingHashKey, int>(capacity: 4, decayInterval: TimeSpan.FromHours(1));

        // The initial miss, losing TryAdd, and follow-up lookup are hash calls 1, 2,
        // and 3. Pause the third so the competing entry can disappear before lookup.
        var requestedKey = new BlockingHashKey(
            1,
            blockOnHashCall: 3,
            hashCallStarted: lookupStarted,
            continueHashCall: continueLookup);

        var getOrAdd = Task.Run(() => cache.GetOrAdd(
            requestedKey,
            static (_, state) =>
            {
                state.FactoryStarted.Set();
                if (!state.ContinueFactory.Wait(TimeSpan.FromSeconds(2)))
                {
                    throw new TimeoutException("The competing cache entry was not inserted.");
                }

                return 101;
            },
            (FactoryStarted: factoryStarted, ContinueFactory: continueFactory)));

        Assert.IsTrue(factoryStarted.Wait(TimeSpan.FromSeconds(2)), "The value factory did not start.");
        cache.Add(new BlockingHashKey(1), 202);
        continueFactory.Set();

        Assert.IsTrue(lookupStarted.Wait(TimeSpan.FromSeconds(2)), "The follow-up cache lookup did not start.");
        Assert.IsTrue(cache.TryRemove(new BlockingHashKey(1)));
        continueLookup.Set();

        Assert.AreEqual(101, await getOrAdd);
        Assert.IsTrue(cache.TryGet(new BlockingHashKey(1), out var cached));
        Assert.AreEqual(101, cached);
    }

    [TestMethod]
    [Timeout(5_000)]
    public void RemovalCallbackReportsReasonValueAndRemainingCount()
    {
        var removals = new ConcurrentQueue<(int Key, int Value, AdaptiveCacheRemovalReason Reason, int Count, int Capacity)>();
        var cache = new AdaptiveCache<int, int>(
            capacity: 1,
            decayInterval: TimeSpan.FromHours(1),
            removalCallback: (key, value, reason, count, capacity) =>
                removals.Enqueue((key, value, reason, count, capacity)));

        cache.Add(1, 101);
        cache.Add(2, 202);

        Assert.IsTrue(
            SpinWait.SpinUntil(
                () => removals.Any(removal => removal.Reason == AdaptiveCacheRemovalReason.Capacity),
                TimeSpan.FromSeconds(2)),
            "Capacity removal was not reported.");
        Assert.IsTrue(removals.TryPeek(out var capacityRemoval));
        Assert.AreEqual(AdaptiveCacheRemovalReason.Capacity, capacityRemoval.Reason);
        Assert.AreEqual(1, capacityRemoval.Count);
        Assert.AreEqual(1, capacityRemoval.Capacity);
        Assert.AreEqual(capacityRemoval.Key == 1 ? 101 : 202, capacityRemoval.Value);

        var remainingKey = cache.TryGet(1, out _) ? 1 : 2;
        var replacedValue = remainingKey == 1 ? 101 : 202;
        cache.Add(remainingKey, 303);
        Assert.IsTrue(removals.Any(
            removal =>
                removal.Reason == AdaptiveCacheRemovalReason.Replaced &&
                removal.Key == remainingKey &&
                removal.Value == replacedValue &&
                removal.Count == 1));
        Assert.IsTrue(cache.TryGet(remainingKey, out var replacement));
        Assert.AreEqual(303, replacement);

        Assert.IsTrue(cache.TryRemove(remainingKey));
        Assert.IsTrue(removals.Any(removal => removal.Reason == AdaptiveCacheRemovalReason.Explicit));
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

    private sealed class BlockingKey : IEquatable<BlockingKey>
    {
        private readonly ManualResetEventSlim? _comparisonStarted;
        private readonly ManualResetEventSlim? _continueComparison;

        public int Value { get; }

        public BlockingKey(
            int value,
            ManualResetEventSlim? comparisonStarted = null,
            ManualResetEventSlim? continueComparison = null)
        {
            Value = value;
            _comparisonStarted = comparisonStarted;
            _continueComparison = continueComparison;
        }

        public bool Equals(BlockingKey? other)
        {
            if (other is null)
            {
                return false;
            }

            var blockingKey = _comparisonStarted is not null ? this : other;
            if (blockingKey._comparisonStarted is not null)
            {
                blockingKey._comparisonStarted.Set();
                if (blockingKey._continueComparison?.Wait(TimeSpan.FromSeconds(2)) != true)
                {
                    throw new TimeoutException("The cache lookup key comparison was not released.");
                }
            }

            return Value == other.Value;
        }

        public override bool Equals(object? obj) => Equals(obj as BlockingKey);

        public override int GetHashCode() => Value;
    }

    private sealed class BlockingHashKey : IEquatable<BlockingHashKey>
    {
        private readonly int _blockOnHashCall;
        private readonly ManualResetEventSlim? _hashCallStarted;
        private readonly ManualResetEventSlim? _continueHashCall;
        private int _hashCalls;

        public int Value { get; }

        public BlockingHashKey(
            int value,
            int blockOnHashCall = 0,
            ManualResetEventSlim? hashCallStarted = null,
            ManualResetEventSlim? continueHashCall = null)
        {
            Value = value;
            _blockOnHashCall = blockOnHashCall;
            _hashCallStarted = hashCallStarted;
            _continueHashCall = continueHashCall;
        }

        public bool Equals(BlockingHashKey? other) => other is not null && Value == other.Value;

        public override bool Equals(object? obj) => Equals(obj as BlockingHashKey);

        public override int GetHashCode()
        {
            if (Interlocked.Increment(ref _hashCalls) == _blockOnHashCall)
            {
                _hashCallStarted?.Set();
                if (_continueHashCall?.Wait(TimeSpan.FromSeconds(2)) != true)
                {
                    throw new TimeoutException("The cache lookup hash calculation was not released.");
                }
            }

            return Value;
        }
    }
}
