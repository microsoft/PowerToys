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
    private sealed class Target : IBatchUpdateTarget
    {
        private int _queued;
        private int _applications;

        public TaskScheduler UIScheduler => TaskScheduler.Default;

        public TaskCompletionSource<bool> Applied { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool TryMarkBatchQueued() => Interlocked.Exchange(ref _queued, 1) == 0;

        public void ClearBatchQueued() => Interlocked.Exchange(ref _queued, 0);

        public void ApplyPendingUpdates()
        {
            if (Interlocked.Increment(ref _applications) == 1)
            {
                // An update arriving during a flush must schedule the following batch.
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
    public async Task UpdateDuringFlush_IsDeliveredOnBackgroundThread()
    {
        var target = new Target();
        BatchUpdateManager.Queue(target);

        Assert.IsTrue(await target.Applied.Task.WaitAsync(TimeSpan.FromSeconds(5)));
    }
}
