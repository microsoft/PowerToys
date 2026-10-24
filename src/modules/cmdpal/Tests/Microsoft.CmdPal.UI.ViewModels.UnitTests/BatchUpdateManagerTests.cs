// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public sealed class BatchUpdateManagerTests
{
    private sealed class Target(int requiredApplications = 2) : IBatchUpdateTarget
    {
        private int _queued;
        private int _applications;

        public TaskScheduler UIScheduler => TaskScheduler.Default;

        public TaskCompletionSource<bool> Applied { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int Applications => Volatile.Read(ref _applications);

        public bool TryMarkBatchQueued() => Interlocked.Exchange(ref _queued, 1) == 0;

        public void ClearBatchQueued() => Interlocked.Exchange(ref _queued, 0);

        public void ApplyPendingUpdates()
        {
            if (Interlocked.Increment(ref _applications) < requiredApplications)
            {
                // Requeueing must survive both the cascade pass and the following timer flush.
                BatchUpdateManager.Queue(this);
            }
            else
            {
                Applied.TrySetResult(Thread.CurrentThread.IsThreadPoolThread);
            }
        }
    }

    [TestMethod]
    public void BatchWindow_FitsWithinOneSixtyHertzFrame()
    {
        var delay = BatchUpdateManager.BatchDelay;
        Assert.IsTrue(delay > TimeSpan.Zero);
        Assert.IsTrue(delay.TotalMilliseconds <= 16.667);
    }

    [TestMethod]
    [DataRow(2)]
    [DataRow(5)]
    public async Task UpdateDuringFlush_IsDeliveredOnBackgroundThread(int applications)
    {
        var target = new Target(applications);
        BatchUpdateManager.Queue(target);

        Assert.IsTrue(await target.Applied.Task.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.AreEqual(applications, target.Applications);
    }
}
