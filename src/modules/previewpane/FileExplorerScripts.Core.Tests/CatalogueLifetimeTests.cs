// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using FileExplorerScripts;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FileExplorerScripts.Core.Tests;

[TestClass]
[DoNotParallelize]
public sealed class CatalogueLifetimeTests
{
    [TestMethod]
    public void StopSignalledBeforeStartupPreventsWarmupAndRegistration()
    {
        using var stop = CreateStopEvent();
        stop.Set();
        using var lifetime = new CatalogueLifetime(Environment.ProcessId);
        Assert.IsTrue(lifetime.Token.IsCancellationRequested);
    }

    [TestMethod]
    public async Task StopDuringWarmupCancelsWithoutWaitingForCatalogueStartup()
    {
        using var stop = CreateStopEvent();
        using var lifetime = new CatalogueLifetime(Environment.ProcessId);
        Assert.IsFalse(lifetime.Token.IsCancellationRequested);
        var warmup = Task.Delay(Timeout.Infinite, lifetime.Token);
        stop.Set();
        await Assert.ThrowsExactlyAsync<TaskCanceledException>(() => warmup.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    private static EventWaitHandle CreateStopEvent()
    {
        var stop = new EventWaitHandle(false, EventResetMode.ManualReset, @"Local\PowerToys.FileExplorerScripts.Stop." + Environment.ProcessId, out var created);
        Assert.IsTrue(created);
        return stop;
    }
}
