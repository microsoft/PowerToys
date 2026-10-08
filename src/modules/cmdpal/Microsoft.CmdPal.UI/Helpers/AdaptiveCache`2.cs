// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Microsoft.CmdPal.Common.Helpers;

namespace Microsoft.CmdPal.UI.Helpers;

/// <summary>
/// A high-performance, near-lock-free adaptive cache optimized for UI Icons.
/// Eviction merely drops references to allow the GC to manage UI-bound lifetimes.
/// </summary>
internal sealed class AdaptiveCache<TKey, TValue>
    where TKey : IEquatable<TKey>
{
    private readonly int _capacity;
    private readonly double _decayFactor;
    private readonly TimeSpan _decayInterval;
    private readonly Action<TKey, TValue, AdaptiveCacheRemovalReason, int, int>? _removalCallback;

    private readonly ConcurrentDictionary<TKey, CacheEntry> _map;
    private readonly WaitCallback _maintenanceCallback;

    // ConcurrentDictionary.Count acquires every stripe lock. Keep an approximate count so
    // cache maintenance never makes the XAML UI thread wait for all dictionary locks.
    private int _entryCount;
    private long _currentTick;
    private long _lastDecayTicks = DateTime.UtcNow.Ticks;
    private InterlockedBoolean _maintenanceSwitch = new(false);

    internal int ApproximateCount => Volatile.Read(ref _entryCount);

    public AdaptiveCache(
        int capacity = 384,
        TimeSpan? decayInterval = null,
        double decayFactor = 0.5,
        Action<TKey, TValue, AdaptiveCacheRemovalReason, int, int>? removalCallback = null)
    {
        _capacity = capacity;
        _decayInterval = decayInterval ?? TimeSpan.FromMinutes(5);
        _decayFactor = decayFactor;
        _removalCallback = removalCallback;
        _map = new ConcurrentDictionary<TKey, CacheEntry>(Environment.ProcessorCount, capacity);

        _maintenanceCallback = static state =>
        {
            var cache = (AdaptiveCache<TKey, TValue>)state!;
            try
            {
                cache.PerformCleanup();
            }
            finally
            {
                cache._maintenanceSwitch.Clear();
            }
        };
    }

    public TValue GetOrAdd<TArg>(TKey key, Func<TKey, TArg, TValue> factory, TArg arg)
    {
        if (_map.TryGetValue(key, out var entry))
        {
            entry.Update(Interlocked.Increment(ref _currentTick));
            return entry.Value!;
        }

        var value = factory(key, arg);
        var tick = Interlocked.Increment(ref _currentTick);
        var newEntry = new CacheEntry(value, 1.0, tick);

        while (!_map.TryAdd(key, newEntry))
        {
            if (_map.TryGetValue(key, out var existing))
            {
                existing.Update(tick);
                return existing.Value!;
            }

            // The entry that defeated TryAdd was removed before the follow-up lookup.
            // Retry with the value we already created rather than returning it uncached.
        }

        Interlocked.Increment(ref _entryCount);

        if (ShouldMaintenanceRun())
        {
            TryRunMaintenance();
        }

        return value;
    }

    public bool TryGet(TKey key, [MaybeNullWhen(false)] out TValue value)
    {
        if (_map.TryGetValue(key, out var entry))
        {
            entry.Update(Interlocked.Increment(ref _currentTick));
            value = entry.Value;
            return true;
        }

        value = default;
        return false;
    }

    public void Add(TKey key, TValue value)
    {
        var tick = Interlocked.Increment(ref _currentTick);
        var newEntry = new CacheEntry(value, 1.0, tick);

        while (true)
        {
            if (_map.TryGetValue(key, out var existing))
            {
                // Add is also an access. Carry the adaptive history into the immutable
                // replacement instead of making a hot key look newly inserted.
                var replacement = new CacheEntry(value, existing.GetFrequency() + 1.0, tick);
                if (_map.TryUpdate(key, replacement, existing))
                {
                    _removalCallback?.Invoke(
                        key,
                        existing.Value,
                        AdaptiveCacheRemovalReason.Replaced,
                        ApproximateCount,
                        _capacity);
                    break;
                }

                continue;
            }

            if (_map.TryAdd(key, newEntry))
            {
                Interlocked.Increment(ref _entryCount);
                break;
            }
        }

        if (ShouldMaintenanceRun())
        {
            TryRunMaintenance();
        }
    }

    public bool TryRemove(TKey key) => TryRemove(key, AdaptiveCacheRemovalReason.Explicit, expected: null);

    private bool TryRemove(TKey key, AdaptiveCacheRemovalReason reason, CacheEntry? expected)
    {
        CacheEntry evicted;
        if (expected is null)
        {
            if (!_map.TryRemove(key, out evicted!))
            {
                return false;
            }
        }
        else if (!_map.TryRemove(new KeyValuePair<TKey, CacheEntry>(key, expected)))
        {
            return false;
        }
        else
        {
            evicted = expected;
        }

        Interlocked.Decrement(ref _entryCount);
        _removalCallback?.Invoke(key, evicted.Value, reason, ApproximateCount, _capacity);
        return true;
    }

    public void Clear()
    {
        // Enumerate the dictionary rather than _map.Keys: the enumerator is lock-free,
        // while Keys snapshots under every stripe lock.
        foreach (var (key, _) in _map)
        {
            TryRemove(key, AdaptiveCacheRemovalReason.Clear, expected: null);
        }

        Interlocked.Exchange(ref _currentTick, 0);
    }

    private bool ShouldMaintenanceRun()
    {
        return ApproximateCount > _capacity || (DateTime.UtcNow.Ticks - Interlocked.Read(ref _lastDecayTicks)) > _decayInterval.Ticks;
    }

    private void TryRunMaintenance()
    {
        if (_maintenanceSwitch.Set())
        {
            ThreadPool.UnsafeQueueUserWorkItem(_maintenanceCallback, this);
        }
    }

    private void PerformCleanup()
    {
        var nowTicks = DateTime.UtcNow.Ticks;
        var isDecay = (nowTicks - Interlocked.Read(ref _lastDecayTicks)) > _decayInterval.Ticks;
        if (isDecay)
        {
            Interlocked.Exchange(ref _lastDecayTicks, nowTicks);
        }

        var currentTick = Interlocked.Read(ref _currentTick);

        foreach (var (key, entry) in _map)
        {
            if (isDecay)
            {
                entry.Decay(_decayFactor);
            }

            var score = CalculateScore(entry, currentTick);

            var overCapacity = ApproximateCount > _capacity;
            if (score < 0.1 || overCapacity)
            {
                TryRemove(
                    key,
                    overCapacity ? AdaptiveCacheRemovalReason.Capacity : AdaptiveCacheRemovalReason.LowScore,
                    entry);
            }
        }
    }

    /// <summary>
    /// Calculates the survival score of an entry.
    /// Higher score = stay in cache; Lower score = priority for eviction.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double CalculateScore(CacheEntry entry, long currentTick)
    {
        // Tuning parameter: How much weight to give recency vs frequency.
        // - a larger ageWeight makes the cache behave more like LRU (Least Recently Used).
        // - a smaller ageWeight makes it behave more like LFU (Least Frequently Used).
        const double ageWeight = 0.001;

        var frequency = entry.GetFrequency();
        var age = currentTick - entry.GetLastAccess();

        return frequency - (age * ageWeight);
    }

    /// <summary>
    /// Represents an immutable cached value with atomic adaptive-eviction metadata.
    /// Entries are never reused: a lock-free reader may retain an entry after the
    /// dictionary has concurrently removed it.
    /// </summary>
    private sealed class CacheEntry
    {
        /// <summary>
        /// Gets the cached value.
        /// </summary>
        public TValue Value { get; }

        /// <summary>
        /// Stores the frequency count as double bits to allow for Interlocked atomic math.
        /// Frequencies are decayed over time to ensure the cache adapts to new usage patterns.
        /// </summary>
        /// <remarks>
        /// This allows the use of Interlocked.CompareExchange to perform thread-safe floating point
        /// arithmetic without a global lock.
        /// </remarks>
        private long _frequencyBits;

        /// <summary>
        /// The tick (monotonically increasing counter) of the last time this entry was accessed.
        /// </summary>
        private long _lastAccessTick;

        public CacheEntry(TValue value, double frequency, long lastAccessTick)
        {
            Value = value;
            _frequencyBits = BitConverter.DoubleToInt64Bits(frequency);
            _lastAccessTick = lastAccessTick;
        }

        public void Update(long tick)
        {
            Interlocked.Exchange(ref _lastAccessTick, tick);
            long initial, updated;
            do
            {
                initial = Interlocked.Read(ref _frequencyBits);
                updated = BitConverter.DoubleToInt64Bits(BitConverter.Int64BitsToDouble(initial) + 1.0);
            }
            while (Interlocked.CompareExchange(ref _frequencyBits, updated, initial) != initial);
        }

        public void Decay(double factor)
        {
            long initial, updated;
            do
            {
                initial = Interlocked.Read(ref _frequencyBits);
                updated = BitConverter.DoubleToInt64Bits(BitConverter.Int64BitsToDouble(initial) * factor);
            }
            while (Interlocked.CompareExchange(ref _frequencyBits, updated, initial) != initial);
        }

        public double GetFrequency()
        {
            return BitConverter.Int64BitsToDouble(Interlocked.Read(ref _frequencyBits));
        }

        public long GetLastAccess()
        {
            return Interlocked.Read(ref _lastAccessTick);
        }
    }
}
