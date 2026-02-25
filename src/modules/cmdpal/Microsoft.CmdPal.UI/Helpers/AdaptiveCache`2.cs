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

    private readonly ConcurrentDictionary<TKey, CacheEntry> _map;
    private readonly ConcurrentStack<CacheEntry> _pool = [];
    private readonly WaitCallback _maintenanceCallback;

    private long _currentTick;
    private long _lastDecayTicks = DateTime.UtcNow.Ticks;
    private InterlockedBoolean _maintenanceSwitch = new(false);

    public AdaptiveCache(int capacity = 384, TimeSpan? decayInterval = null, double decayFactor = 0.5)
    {
        _capacity = capacity;
        _decayInterval = decayInterval ?? TimeSpan.FromMinutes(5);
        _decayFactor = decayFactor;
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

        if (!_pool.TryPop(out var newEntry))
        {
            newEntry = new CacheEntry();
        }

        var value = factory(key, arg);
        var tick = Interlocked.Increment(ref _currentTick);
        newEntry.Initialize(key, value, 1.0, tick);

        if (!_map.TryAdd(key, newEntry))
        {
            newEntry.Clear();
            _pool.Push(newEntry);

            if (_map.TryGetValue(key, out var existing))
            {
                existing.Update(tick);
                return existing.Value!;
            }
        }

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

        if (_map.TryGetValue(key, out var existing))
        {
            existing.Update(tick);
            existing.SetValue(value);
            return;
        }

        if (!_pool.TryPop(out var newEntry))
        {
            newEntry = new CacheEntry();
        }

        newEntry.Initialize(key, value, 1.0, tick);

        if (!_map.TryAdd(key, newEntry))
        {
            newEntry.Clear();
            _pool.Push(newEntry);
        }

        if (ShouldMaintenanceRun())
        {
            TryRunMaintenance();
        }
    }

    public bool TryRemove(TKey key)
    {
        if (_map.TryRemove(key, out var evicted))
        {
            evicted.Clear();
            _pool.Push(evicted);
            return true;
        }

        return false;
    }

    public void Clear()
    {
        foreach (var key in _map.Keys)
        {
            TryRemove(key);
        }

        Interlocked.Exchange(ref _currentTick, 0);
    }

    private bool ShouldMaintenanceRun()
    {
        return _map.Count > _capacity || (DateTime.UtcNow.Ticks - Interlocked.Read(ref _lastDecayTicks)) > _decayInterval.Ticks;
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

            if (score < 0.1 || _map.Count > _capacity)
            {
                if (_map.TryRemove(key, out var evicted))
                {
                    evicted.Clear();
                    _pool.Push(evicted);
                }
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
    /// Represents a single pooled entry in the cache, containing the value and
    /// atomic metadata for adaptive eviction logic.
    /// </summary>
    private sealed class CacheEntry
    {
        /// <summary>
        /// Gets the key associated with this entry. Used primarily for identification during cleanup.
        /// </summary>
        public TKey Key { get; private set; } = default!;

        /// <summary>
        /// Gets the cached value. This reference is cleared on eviction to allow GC collection.
        /// </summary>
        public TValue Value { get; private set; } = default!;

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

        public void Initialize(TKey key, TValue value, double frequency, long lastAccessTick)
        {
            Key = key;
            Value = value;
            _frequencyBits = BitConverter.DoubleToInt64Bits(frequency);
            _lastAccessTick = lastAccessTick;
        }

        public void SetValue(TValue value)
        {
            Value = value;
        }

        public void Clear()
        {
            Key = default!;
            Value = default!;
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
