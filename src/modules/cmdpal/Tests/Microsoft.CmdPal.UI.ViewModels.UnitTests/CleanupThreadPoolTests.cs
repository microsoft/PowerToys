// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[OSCondition(OperatingSystems.Windows)]
[TestClass]
public class CleanupThreadPoolTests
{
    [TestMethod]
    public async Task Queue_KeepsCleanupTrackedUntilItFinishes()
    {
        using var release = new ManualResetEventSlim();
        var description = Guid.NewGuid().ToString();
        var completion = CleanupThreadPool.Queue(() => release.Wait(TimeSpan.FromSeconds(10), TestContext.CancellationToken), description);
        try
        {
            Assert.IsFalse(completion.IsCompleted);
            Assert.Contains(work => work.Description == description, CleanupThreadPool.Pending);
        }
        finally
        {
            release.Set();
            await completion.WaitAsync(TimeSpan.FromSeconds(2), System.Threading.CancellationToken.None);
        }

        Assert.DoesNotContain(work => work.Description == description, CleanupThreadPool.Pending);
    }

    public TestContext TestContext { get; set; } = null!;
}
