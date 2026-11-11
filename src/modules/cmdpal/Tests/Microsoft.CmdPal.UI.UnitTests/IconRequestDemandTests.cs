// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.UnitTests;

[OSCondition(OperatingSystems.Windows)]
[TestClass]
public class IconRequestDemandTests
{
    [TestMethod]
    public void ReleaseBeforeAttachLeavesLoadSpeculative()
    {
        var request = new IconRequestDemand();
        var load = new IconLoadDemand();

        request.Release();
        request.Attach(load);

        Assert.IsFalse(load.IsDemanded);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "MSTEST0045:Use cooperative cancellation for timeout", Justification = "Synchronous and native operations require the independent MSTest timeout watchdog.")]
    [TestMethod]
    [Timeout(5_000, CooperativeCancellation = false)]
    public void ConcurrentAttachAndReleaseNeverLeaksDemand()
    {
        for (var i = 0; i < 1_000; i++)
        {
            var request = new IconRequestDemand();
            var load = new IconLoadDemand();

            Parallel.Invoke(
                () => request.Attach(load),
                request.Release);

            Assert.IsFalse(load.IsDemanded);
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "MSTEST0045:Use cooperative cancellation for timeout", Justification = "Synchronous and native operations require the independent MSTest timeout watchdog.")]
    [TestMethod]
    [Timeout(5_000, CooperativeCancellation = false)]
    public void DelayedNotificationObservesCurrentDemand()
    {
        var load = new IconLoadDemand();
        using var firstNotificationStarted = new ManualResetEventSlim();
        using var resumeFirstNotification = new ManualResetEventSlim();
        var observer = new DelayedObserver(load, firstNotificationStarted, resumeFirstNotification);
        load.Subscribe(observer);
        Assert.IsFalse(load.IsDemanded);

        var addRequester = Task.Run(load.AddRequester, TestContext.CancellationToken);
        try
        {
            Assert.IsTrue(firstNotificationStarted.Wait(TimeSpan.FromSeconds(2), TestContext.CancellationToken));
            load.RemoveRequester();
        }
        finally
        {
            resumeFirstNotification.Set();
            addRequester.GetAwaiter().GetResult();
        }

        Assert.IsFalse(observer.LastObservedDemand);
        load.Unsubscribe(observer);
    }

    private sealed class DelayedObserver(
        IconLoadDemand demand,
        ManualResetEventSlim firstNotificationStarted,
        ManualResetEventSlim resumeFirstNotification) : IconLoadDemand.IObserver
    {
        private int _notificationCount;
        private int _lastObservedDemand;

        public bool LastObservedDemand => Volatile.Read(ref _lastObservedDemand) != 0;

        public void OnDemandChanged()
        {
            if (Interlocked.Increment(ref _notificationCount) == 1)
            {
                firstNotificationStarted.Set();
                resumeFirstNotification.Wait();
            }

            Volatile.Write(ref _lastObservedDemand, demand.IsDemanded ? 1 : 0);
        }
    }

    public TestContext TestContext { get; set; } = null!;
}
