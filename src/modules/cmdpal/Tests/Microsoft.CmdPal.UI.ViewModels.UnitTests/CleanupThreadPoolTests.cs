// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public class CleanupThreadPoolTests
{
    [TestMethod]
    public async Task Queue_KeepsCleanupTrackedUntilItFinishes()
    {
        using var release = new ManualResetEventSlim();
        var description = Guid.NewGuid().ToString();
        var completion = CleanupThreadPool.Queue(() => release.Wait(TimeSpan.FromSeconds(10)), description);
        try
        {
            Assert.IsFalse(completion.IsCompleted);
            Assert.IsTrue(CleanupThreadPool.Pending.Any(work => work.Description == description));
        }
        finally
        {
            release.Set();
            await completion.WaitAsync(TimeSpan.FromSeconds(2));
        }

        Assert.IsFalse(CleanupThreadPool.Pending.Any(work => work.Description == description));
    }
}
