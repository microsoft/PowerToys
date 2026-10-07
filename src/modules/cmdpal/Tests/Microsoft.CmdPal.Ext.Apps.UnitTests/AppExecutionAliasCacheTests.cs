// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.Apps.Win32;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Apps.UnitTests;

[TestClass]
public class AppExecutionAliasCacheTests
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(5);

    [TestMethod]
    [Timeout(10_000)]
    public async Task RequestRefresh_BlockedColdReadDoesNotBlockCallersOrDuplicateWork()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var readCount = 0;
        using var cache = new AppExecutionAliasCache(
            () =>
            {
                Interlocked.Increment(ref readCount);
                entered.TrySetResult();
                release.Task.GetAwaiter().GetResult();
                return new Dictionary<string, string> { ["wt.exe"] = "Terminal_123!App" };
            },
            new TestTimeProvider(),
            RefreshInterval);

        Assert.IsTrue(cache.GetSnapshot().IsEmpty);
        Assert.AreEqual(0, readCount, "Reading a snapshot must not start native or filesystem work.");

        try
        {
            await Task.Run(cache.RequestRefresh).WaitAsync(TestTimeout);
            await entered.Task.WaitAsync(TestTimeout);

            Assert.IsFalse(cache.PendingRefresh.IsCompleted);
            for (var i = 0; i < 5; i++)
            {
                Assert.IsTrue(cache.GetSnapshot().IsEmpty);
                cache.RequestRefresh();
            }

            Assert.AreEqual(1, Volatile.Read(ref readCount), "Requests must share the blocked refresh.");
        }
        finally
        {
            release.TrySetResult();
        }

        await cache.PendingRefresh.WaitAsync(TestTimeout);
        Assert.AreEqual("Terminal_123!App", cache.GetSnapshot()["WT.EXE"]);
        Assert.AreEqual(1, readCount);
    }

    [TestMethod]
    [Timeout(10_000)]
    public async Task RequestRefresh_UsesTtlAndPreservesEarlierSnapshotsWhenOwnerChanges()
    {
        var timeProvider = new TestTimeProvider();
        var owner = "Terminal_123!App";
        var readCount = 0;
        var changedCount = 0;
        using var cache = new AppExecutionAliasCache(
            () =>
            {
                readCount++;
                return new Dictionary<string, string> { ["wt.exe"] = owner };
            },
            timeProvider,
            RefreshInterval);
        cache.Changed += (_, _) => changedCount++;

        cache.RequestRefresh();
        await cache.PendingRefresh.WaitAsync(TestTimeout);
        var earlier = cache.GetSnapshot();

        owner = "TerminalPreview_123!App";
        timeProvider.Advance(RefreshInterval - TimeSpan.FromTicks(1));
        for (var i = 0; i < 5; i++)
        {
            cache.RequestRefresh();
        }

        await cache.PendingRefresh.WaitAsync(TestTimeout);
        Assert.AreEqual(1, readCount);
        Assert.AreEqual("Terminal_123!App", cache.GetSnapshot()["wt.exe"]);

        timeProvider.Advance(TimeSpan.FromTicks(1));
        cache.RequestRefresh();
        await cache.PendingRefresh.WaitAsync(TestTimeout);

        Assert.AreEqual(2, readCount);
        Assert.AreEqual(2, changedCount);
        Assert.AreEqual("TerminalPreview_123!App", cache.GetSnapshot()["WT.EXE"]);
        Assert.AreEqual("Terminal_123!App", earlier["wt.exe"], "A running search must retain its captured owner map.");
    }

    [TestMethod]
    [Timeout(10_000)]
    public async Task RequestRefresh_OnlyPublishesContentChangesIncludingRemovedAliases()
    {
        var timeProvider = new TestTimeProvider();
        IReadOnlyDictionary<string, string> owners = new Dictionary<string, string> { ["wt.exe"] = "Terminal_123!App" };
        var changedCount = 0;
        using var cache = new AppExecutionAliasCache(() => owners, timeProvider, RefreshInterval);
        cache.Changed += (_, _) => changedCount++;

        cache.RequestRefresh();
        await cache.PendingRefresh.WaitAsync(TestTimeout);
        var earlier = cache.GetSnapshot();

        owners = new Dictionary<string, string> { ["WT.EXE"] = "Terminal_123!App" };
        timeProvider.Advance(RefreshInterval);
        cache.RequestRefresh();
        await cache.PendingRefresh.WaitAsync(TestTimeout);

        Assert.AreEqual(1, changedCount, "Filename casing must not create a new ownership publication.");
        Assert.AreSame(earlier, cache.GetSnapshot());

        owners = new Dictionary<string, string>();
        timeProvider.Advance(RefreshInterval);
        cache.RequestRefresh();
        await cache.PendingRefresh.WaitAsync(TestTimeout);

        Assert.AreEqual(2, changedCount);
        Assert.IsTrue(cache.GetSnapshot().IsEmpty, "A successful scan must remove disabled or deleted aliases.");
        Assert.AreEqual("Terminal_123!App", earlier["wt.exe"]);
    }

    [TestMethod]
    [Timeout(10_000)]
    public async Task RequestRefresh_ReadFailureRetainsLastOwnersAndAllowsLaterRetry()
    {
        var timeProvider = new TestTimeProvider();
        var failRead = false;
        var owner = "Terminal_123!App";
        var changedCount = 0;
        using var cache = new AppExecutionAliasCache(
            () =>
            {
                if (failRead)
                {
                    throw new InvalidOperationException("Alias read failed.");
                }

                return new Dictionary<string, string> { ["wt.exe"] = owner };
            },
            timeProvider,
            RefreshInterval);
        cache.Changed += (_, _) => changedCount++;

        cache.RequestRefresh();
        await cache.PendingRefresh.WaitAsync(TestTimeout);
        var earlier = cache.GetSnapshot();

        failRead = true;
        timeProvider.Advance(RefreshInterval);
        cache.RequestRefresh();
        await cache.PendingRefresh.WaitAsync(TestTimeout);

        Assert.AreSame(earlier, cache.GetSnapshot());
        Assert.AreEqual(1, changedCount);

        failRead = false;
        owner = "TerminalPreview_123!App";
        timeProvider.Advance(RefreshInterval);
        cache.RequestRefresh();
        await cache.PendingRefresh.WaitAsync(TestTimeout);

        Assert.AreEqual("TerminalPreview_123!App", cache.GetSnapshot()["wt.exe"]);
        Assert.AreEqual(2, changedCount);
    }

    [TestMethod]
    [Timeout(10_000)]
    public async Task Dispose_DuringBlockedReadSuppressesLatePublicationAndFurtherRefreshes()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var readCount = 0;
        var changedCount = 0;
        using var cache = new AppExecutionAliasCache(
            () =>
            {
                Interlocked.Increment(ref readCount);
                entered.TrySetResult();
                release.Task.GetAwaiter().GetResult();
                return new Dictionary<string, string> { ["wt.exe"] = "Terminal_123!App" };
            },
            new TestTimeProvider(),
            RefreshInterval);
        cache.Changed += (_, _) => changedCount++;
        cache.RequestRefresh();
        var pending = cache.PendingRefresh;

        try
        {
            await entered.Task.WaitAsync(TestTimeout);
            cache.Dispose();
            cache.RequestRefresh();
            Assert.IsTrue(cache.GetSnapshot().IsEmpty);
        }
        finally
        {
            release.TrySetResult();
        }

        await pending.WaitAsync(TestTimeout);
        cache.RequestRefresh();

        Assert.IsTrue(cache.GetSnapshot().IsEmpty);
        Assert.AreEqual(0, changedCount);
        Assert.AreEqual(1, readCount);
    }

    [TestMethod]
    [Timeout(10_000)]
    public async Task RequestRefresh_ThrowingListenerDoesNotSkipLaterListenersOrFuturePublications()
    {
        var timeProvider = new TestTimeProvider();
        var owner = "Terminal_123!App";
        var changedCount = 0;
        using var cache = new AppExecutionAliasCache(
            () => new Dictionary<string, string> { ["wt.exe"] = owner },
            timeProvider,
            RefreshInterval);
        cache.Changed += (_, _) => throw new InvalidOperationException("Alias listener failed.");
        cache.Changed += (_, _) => changedCount++;

        cache.RequestRefresh();
        await cache.PendingRefresh.WaitAsync(TestTimeout);
        Assert.AreEqual(1, changedCount);
        Assert.AreEqual("Terminal_123!App", cache.GetSnapshot()["wt.exe"]);

        owner = "TerminalPreview_123!App";
        timeProvider.Advance(RefreshInterval);
        cache.RequestRefresh();
        await cache.PendingRefresh.WaitAsync(TestTimeout);

        Assert.AreEqual(2, changedCount);
        Assert.AreEqual("TerminalPreview_123!App", cache.GetSnapshot()["wt.exe"]);
    }

    [TestMethod]
    [Timeout(10_000)]
    public async Task RequestRefresh_CreatesWatcherLazilyInBackgroundBeforeReadingOwners()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sequence = 0;
        var watcherCreatedAt = 0;
        var ownersReadAt = 0;
        using var cache = new AppExecutionAliasCache(
            () =>
            {
                ownersReadAt = Interlocked.Increment(ref sequence);
                return new Dictionary<string, string> { ["wt.exe"] = "Terminal_123!App" };
            },
            new TestTimeProvider(),
            RefreshInterval,
            createWatcher: (changed, error) =>
            {
                entered.TrySetResult();
                release.Task.GetAwaiter().GetResult();
                watcherCreatedAt = Interlocked.Increment(ref sequence);
                return new TestWatcher(changed, error);
            });

        Assert.IsTrue(cache.GetSnapshot().IsEmpty);
        Assert.AreEqual(0, sequence);
        try
        {
            await Task.Run(cache.RequestRefresh).WaitAsync(TestTimeout);
            await entered.Task.WaitAsync(TestTimeout);
            Assert.IsTrue(cache.GetSnapshot().IsEmpty);
            Assert.AreEqual(0, sequence, "Neither query access nor a blocked watcher factory may read aliases.");
        }
        finally
        {
            release.TrySetResult();
        }

        await cache.PendingRefresh.WaitAsync(TestTimeout);
        Assert.AreEqual(1, watcherCreatedAt);
        Assert.AreEqual(2, ownersReadAt, "The watcher must be attached before the initial scan.");
        Assert.AreEqual("Terminal_123!App", cache.GetSnapshot()["wt.exe"]);
    }

    [TestMethod]
    [Timeout(10_000)]
    public async Task WatcherChanges_DebounceBurstsAndStopPendingWorkOnDispose()
    {
        var timeProvider = new TestTimeProvider();
        var debounce = TimeSpan.FromMilliseconds(100);
        var readCount = 0;
        var owner = "Terminal_123!App";
        TestWatcher watcher = null;
        using var cache = new AppExecutionAliasCache(
            () =>
            {
                readCount++;
                return new Dictionary<string, string> { ["wt.exe"] = owner };
            },
            timeProvider,
            RefreshInterval,
            createWatcher: (changed, error) => watcher = new TestWatcher(changed, error),
            debounceInterval: debounce);
        cache.RequestRefresh();
        await cache.PendingRefresh.WaitAsync(TestTimeout);

        owner = "TerminalPreview_123!App";
        var timerScheduled = timeProvider.ExpectTimer();
        for (var i = 0; i < 5; i++)
        {
            watcher.Change();
        }

        await timerScheduled.WaitAsync(TestTimeout);
        var pending = cache.PendingRefresh;
        Assert.IsFalse(pending.IsCompleted);
        timeProvider.Advance(debounce - TimeSpan.FromTicks(1));
        Assert.AreEqual(1, readCount);
        timeProvider.Advance(TimeSpan.FromTicks(1));
        await pending.WaitAsync(TestTimeout);

        Assert.AreEqual(2, readCount, "An event burst must force one scan even within the safety-refresh interval.");
        Assert.AreEqual("TerminalPreview_123!App", cache.GetSnapshot()["wt.exe"]);

        timerScheduled = timeProvider.ExpectTimer();
        watcher.Change();
        await timerScheduled.WaitAsync(TestTimeout);
        pending = cache.PendingRefresh;
        cache.Dispose();
        timeProvider.Advance(debounce);
        await pending.WaitAsync(TestTimeout);
        watcher.Change();

        Assert.AreEqual(1, watcher.DisposeCount);
        Assert.AreEqual(2, readCount, "A pending event must not read aliases after disposal.");
    }

    [TestMethod]
    [Timeout(10_000)]
    public async Task WatcherChanges_DuringReadGuaranteeOneFollowUpScan()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var owner = "Terminal_123!App";
        var readCount = 0;
        TestWatcher watcher = null;
        using var cache = new AppExecutionAliasCache(
            () =>
            {
                var capturedOwner = owner;
                if (Interlocked.Increment(ref readCount) == 2)
                {
                    entered.TrySetResult();
                    release.Task.GetAwaiter().GetResult();
                }

                return new Dictionary<string, string> { ["wt.exe"] = capturedOwner };
            },
            new TestTimeProvider(),
            RefreshInterval,
            createWatcher: (changed, error) => watcher = new TestWatcher(changed, error),
            debounceInterval: TimeSpan.Zero);
        cache.RequestRefresh();
        await cache.PendingRefresh.WaitAsync(TestTimeout);

        watcher.Change();
        var pending = cache.PendingRefresh;
        try
        {
            await entered.Task.WaitAsync(TestTimeout);
            owner = "TerminalPreview_123!App";
            for (var i = 0; i < 5; i++)
            {
                watcher.Change();
            }

            Assert.IsFalse(pending.IsCompleted);
        }
        finally
        {
            release.TrySetResult();
        }

        await pending.WaitAsync(TestTimeout);
        Assert.AreEqual(3, readCount, "Events during the blocked read must produce one subsequent scan.");
        Assert.AreEqual("TerminalPreview_123!App", cache.GetSnapshot()["wt.exe"]);
    }

    [TestMethod]
    [DataRow("missing")]
    [DataRow("setup failure")]
    [DataRow("watcher error")]
    [Timeout(10_000)]
    public async Task WatcherRecovery_RetriesOnlyOnSafetyRefresh(string failure)
    {
        var timeProvider = new TestTimeProvider();
        var owner = "Terminal_123!App";
        var factoryCount = 0;
        var readCount = 0;
        TestWatcher firstWatcher = null;
        TestWatcher replacement = null;
        using var cache = new AppExecutionAliasCache(
            () =>
            {
                readCount++;
                return new Dictionary<string, string> { ["wt.exe"] = owner };
            },
            timeProvider,
            RefreshInterval,
            createWatcher: (changed, error) =>
            {
                factoryCount++;
                if (factoryCount == 1)
                {
                    if (failure == "missing")
                    {
                        return null;
                    }

                    if (failure == "setup failure")
                    {
                        throw new IOException("Watcher setup failed.");
                    }

                    firstWatcher = new TestWatcher(changed, error);
                    return firstWatcher;
                }

                replacement = new TestWatcher(changed, error);
                return replacement;
            },
            debounceInterval: TimeSpan.Zero);
        cache.RequestRefresh();
        await cache.PendingRefresh.WaitAsync(TestTimeout);
        Assert.AreEqual("Terminal_123!App", cache.GetSnapshot()["wt.exe"], "Unavailable watching must not prevent discovery.");

        owner = "TerminalPreview_123!App";
        var expectedReads = 1;
        if (failure == "watcher error")
        {
            firstWatcher.Fail();
            await cache.PendingRefresh.WaitAsync(TestTimeout);
            expectedReads++;
            Assert.AreEqual(1, firstWatcher.DisposeCount);
            Assert.AreEqual("TerminalPreview_123!App", cache.GetSnapshot()["wt.exe"]);
        }

        cache.RequestRefresh();
        await cache.PendingRefresh.WaitAsync(TestTimeout);
        Assert.AreEqual(1, factoryCount, "A failed watcher must not cause a recreation loop.");
        Assert.AreEqual(expectedReads, readCount);

        timeProvider.Advance(RefreshInterval);
        cache.RequestRefresh();
        await cache.PendingRefresh.WaitAsync(TestTimeout);
        Assert.AreEqual(2, factoryCount);
        Assert.AreEqual(expectedReads + 1, readCount);
        Assert.AreEqual("TerminalPreview_123!App", cache.GetSnapshot()["wt.exe"]);
        Assert.IsNotNull(replacement);

        if (firstWatcher is not null)
        {
            firstWatcher.Change();
            firstWatcher.Fail();
            await cache.PendingRefresh.WaitAsync(TestTimeout);
            Assert.AreEqual(expectedReads + 1, readCount, "Retired watcher callbacks must not invalidate the replacement.");
            Assert.AreEqual(0, replacement.DisposeCount);
        }
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    [Timeout(10_000)]
    public async Task Dispose_PendingWatcherSetupOrReadCannotPublishOrReactivate(bool duringSetup)
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var readCount = 0;
        var changedCount = 0;
        TestWatcher watcher = null;
        using var cache = new AppExecutionAliasCache(
            () =>
            {
                Interlocked.Increment(ref readCount);
                if (!duringSetup)
                {
                    entered.TrySetResult();
                    release.Task.GetAwaiter().GetResult();
                }

                return new Dictionary<string, string> { ["wt.exe"] = "Terminal_123!App" };
            },
            new TestTimeProvider(),
            RefreshInterval,
            createWatcher: (changed, error) =>
            {
                watcher = new TestWatcher(changed, error);
                if (duringSetup)
                {
                    entered.TrySetResult();
                    release.Task.GetAwaiter().GetResult();
                }

                return watcher;
            },
            debounceInterval: TimeSpan.Zero);
        cache.Changed += (_, _) => changedCount++;
        cache.RequestRefresh();
        var pending = cache.PendingRefresh;

        try
        {
            await entered.Task.WaitAsync(TestTimeout);
            cache.Dispose();
        }
        finally
        {
            release.TrySetResult();
        }

        await pending.WaitAsync(TestTimeout);
        watcher.Change();
        watcher.Fail();
        cache.RequestRefresh();
        await cache.PendingRefresh.WaitAsync(TestTimeout);

        Assert.IsTrue(cache.GetSnapshot().IsEmpty);
        Assert.AreEqual(0, changedCount);
        Assert.AreEqual(duringSetup ? 0 : 1, readCount);
        Assert.AreEqual(1, watcher.DisposeCount);
    }

    [TestMethod]
    [Timeout(10_000)]
    public async Task WatcherSetup_MissingDirectoryScansAndRetriesWithoutWarnings()
    {
        var missingDirectory = Path.Combine(Path.GetTempPath(), "cmdpal-alias-watch-missing-" + Guid.NewGuid().ToString("N"));
        var timeProvider = new TestTimeProvider();
        var logger = new TestLogger();
        var setupCount = 0;
        var readCount = 0;
        using var cache = new AppExecutionAliasCache(
            () =>
            {
                readCount++;
                return new Dictionary<string, string>();
            },
            timeProvider,
            RefreshInterval,
            logger,
            createWatcher: (changed, failed) =>
            {
                setupCount++;
                return AppExecutionAliasCache.CreateWatcher(missingDirectory, changed, failed);
            });

        cache.RequestRefresh();
        await cache.PendingRefresh.WaitAsync(TestTimeout);
        timeProvider.Advance(RefreshInterval);
        cache.RequestRefresh();
        await cache.PendingRefresh.WaitAsync(TestTimeout);

        Assert.AreEqual(2, setupCount);
        Assert.AreEqual(2, readCount, "A missing alias directory must not prevent scans or later monitoring attempts.");
        Assert.IsTrue(cache.GetSnapshot().IsEmpty);
        Assert.AreEqual(0, logger.WarningCount, "A profile without WindowsApps must not produce watcher warnings.");
    }

    [TestMethod]
    [Timeout(10_000)]
    public async Task WatcherChanges_UnexpectedLoopFailureRetainsOwnersAndAllowsLaterEventRetry()
    {
        var timeProvider = new TestTimeProvider();
        var debounce = TimeSpan.FromMilliseconds(100);
        var logger = new TestLogger();
        var owner = "Terminal_123!App";
        var readCount = 0;
        TestWatcher watcher = null;
        using var cache = new AppExecutionAliasCache(
            () =>
            {
                readCount++;
                return new Dictionary<string, string> { ["wt.exe"] = owner };
            },
            timeProvider,
            RefreshInterval,
            logger,
            createWatcher: (changed, error) => watcher = new TestWatcher(changed, error),
            debounceInterval: debounce);
        cache.RequestRefresh();
        await cache.PendingRefresh.WaitAsync(TestTimeout);
        var earlier = cache.GetSnapshot();

        timeProvider.ThrowOnNextTimer = true;
        watcher.Change();
        await cache.PendingRefresh.WaitAsync(TestTimeout);

        Assert.IsFalse(timeProvider.ThrowOnNextTimer);
        Assert.AreSame(earlier, cache.GetSnapshot());
        Assert.AreEqual(1, readCount);
        Assert.AreEqual(1, logger.WarningCount);

        owner = "TerminalPreview_123!App";
        var timerScheduled = timeProvider.ExpectTimer();
        watcher.Change();
        await timerScheduled.WaitAsync(TestTimeout);
        var pending = cache.PendingRefresh;
        timeProvider.Advance(debounce);
        await pending.WaitAsync(TestTimeout);

        Assert.AreEqual(2, readCount, "A failed refresh loop must release its worker state so another event can retry.");
        Assert.AreEqual("TerminalPreview_123!App", cache.GetSnapshot()["wt.exe"]);
        Assert.AreEqual(1, logger.WarningCount);
    }

    private sealed class TestLogger : ILogger<AppExecutionAliasCache>
    {
        private int _warningCount;

        public int WarningCount => Volatile.Read(ref _warningCount);

        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (logLevel == LogLevel.Warning)
            {
                Interlocked.Increment(ref _warningCount);
            }
        }
    }

    private sealed class TestWatcher : IDisposable
    {
        private readonly Action _changed;
        private readonly Action<Exception> _error;

        public int DisposeCount { get; private set; }

        public TestWatcher(Action changed, Action<Exception> error)
        {
            _changed = changed;
            _error = error;
        }

        public void Change()
        {
            _changed();
        }

        public void Fail()
        {
            _error(new IOException("Watcher failed."));
        }

        public void Dispose()
        {
            DisposeCount++;
        }
    }

    private sealed class TestTimeProvider : TimeProvider
    {
        private readonly Lock _timerLock = new();
        private readonly List<TestTimer> _timers = [];
        private TaskCompletionSource _timerScheduled = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private long _ticks = new DateTimeOffset(2026, 10, 4, 0, 0, 0, TimeSpan.Zero).Ticks;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public bool ThrowOnNextTimer { get; set; }

        public override DateTimeOffset GetUtcNow()
        {
            return new DateTimeOffset(Interlocked.Read(ref _ticks), TimeSpan.Zero);
        }

        public override long GetTimestamp()
        {
            return Interlocked.Read(ref _ticks);
        }

        public override ITimer CreateTimer(TimerCallback callback, object state, TimeSpan dueTime, TimeSpan period)
        {
            if (ThrowOnNextTimer)
            {
                ThrowOnNextTimer = false;
                throw new InvalidOperationException("Timer creation failed.");
            }

            var timer = new TestTimer(this, callback, state);
            timer.Change(dueTime, period);
            lock (_timerLock)
            {
                _timers.Add(timer);
                _timerScheduled.TrySetResult();
            }

            return timer;
        }

        public Task ExpectTimer()
        {
            lock (_timerLock)
            {
                _timerScheduled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                return _timerScheduled.Task;
            }
        }

        public void Advance(TimeSpan elapsed)
        {
            var timestamp = Interlocked.Add(ref _ticks, elapsed.Ticks);
            TestTimer[] timers;
            lock (_timerLock)
            {
                timers = [.. _timers];
            }

            foreach (var timer in timers)
            {
                timer.FireIfDue(timestamp);
            }
        }
    }

    private sealed class TestTimer : ITimer
    {
        private readonly Lock _stateLock = new();
        private readonly TestTimeProvider _timeProvider;
        private readonly TimerCallback _callback;
        private readonly object _state;
        private long _dueAt = long.MaxValue;
        private bool _disposed;

        public TestTimer(TestTimeProvider timeProvider, TimerCallback callback, object state)
        {
            _timeProvider = timeProvider;
            _callback = callback;
            _state = state;
        }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            if (period != Timeout.InfiniteTimeSpan)
            {
                throw new NotSupportedException("This fixture supports only one-shot timers.");
            }

            lock (_stateLock)
            {
                if (_disposed)
                {
                    return false;
                }

                _dueAt = dueTime == Timeout.InfiniteTimeSpan ? long.MaxValue : _timeProvider.GetTimestamp() + dueTime.Ticks;
                return true;
            }
        }

        public void FireIfDue(long timestamp)
        {
            lock (_stateLock)
            {
                if (_disposed || timestamp < _dueAt)
                {
                    return;
                }

                _dueAt = long.MaxValue;
            }

            _callback(_state);
        }

        public void Dispose()
        {
            lock (_stateLock)
            {
                _disposed = true;
            }
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
