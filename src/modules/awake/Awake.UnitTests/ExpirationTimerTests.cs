// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using Awake.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Awake.UnitTests;

[TestClass]
public class ExpirationTimerTests
{
    [TestMethod]
    public void ExpirationTimer_SixtyDayDeadlineCanSubscribeOnDefaultScheduler()
    {
        using IDisposable subscription = Manager.CreateExpirationTimer(DateTimeOffset.Now.AddDays(60), Scheduler.Default)
            .Subscribe(_ => Assert.Fail("A sixty-day deadline must not expire during subscription."));
    }

    [DataRow(4294967294L, 1)]
    [DataRow(4294967295L, 2)]
    [DataRow(5184000000L, 2)]
    [DataRow(1000L, 1)]
    [DataTestMethod]
    public void ExpirationTimer_BoundsEachWaitAndExpiresOnlyAtDeadline(long milliseconds, int expectedWaits)
    {
        var scheduler = new BoundedScheduler();
        DateTimeOffset deadline = scheduler.Now.AddMilliseconds(milliseconds);
        int notifications = 0;
        bool completed = false;

        Assert.IsTrue(CommandLineArgumentNormalizer.TryNormalize(
            ["until", deadline.ToString("O", CultureInfo.InvariantCulture)],
            scheduler.Now,
            CultureInfo.InvariantCulture,
            out _,
            out _,
            out _));

        using IDisposable subscription = Manager.CreateExpirationTimer(deadline, scheduler)
            .Subscribe(_ => notifications++, () => completed = true);

        scheduler.Clock.AdvanceTo(deadline.AddTicks(-1));
        Assert.AreEqual(0, notifications);
        Assert.IsFalse(completed);

        scheduler.Clock.AdvanceTo(deadline);
        Assert.AreEqual(1, notifications);
        Assert.IsTrue(completed);
        Assert.AreEqual(expectedWaits, scheduler.Waits.Count);

        scheduler.Clock.AdvanceBy(TimeSpan.FromDays(60));
        Assert.AreEqual(1, notifications);
    }

    [TestMethod]
    public void ExpirationTimer_DisposalCancelsRearmedWait()
    {
        var scheduler = new BoundedScheduler();
        DateTimeOffset deadline = scheduler.Now.AddDays(60);
        int notifications = 0;
        IDisposable subscription = Manager.CreateExpirationTimer(deadline, scheduler)
            .Subscribe(_ => notifications++);

        scheduler.Clock.AdvanceBy(TimeSpan.FromMilliseconds(4294967294L));
        Assert.AreEqual(2, scheduler.Waits.Count);
        subscription.Dispose();
        scheduler.Clock.AdvanceTo(deadline);

        Assert.AreEqual(0, notifications);
        Assert.AreEqual(2, scheduler.Waits.Count);
    }

    private sealed class BoundedScheduler : IScheduler
    {
        public HistoricalScheduler Clock { get; } = new(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

        public List<TimeSpan> Waits { get; } = [];

        public DateTimeOffset Now => Clock.Now;

        public IDisposable Schedule<TState>(TState state, Func<IScheduler, TState, IDisposable> action)
        {
            return Schedule(state, TimeSpan.Zero, action);
        }

        public IDisposable Schedule<TState>(TState state, DateTimeOffset dueTime, Func<IScheduler, TState, IDisposable> action)
        {
            return Schedule(state, dueTime - Now, action);
        }

        public IDisposable Schedule<TState>(TState state, TimeSpan dueTime, Func<IScheduler, TState, IDisposable> action)
        {
            Assert.IsTrue(dueTime >= TimeSpan.Zero);
            Assert.IsTrue(dueTime <= TimeSpan.FromMilliseconds(4294967294L), "The native timer's maximum due time must not be exceeded.");
            Waits.Add(dueTime);
            return Clock.Schedule(state, dueTime, (_, value) => action(this, value));
        }
    }
}
