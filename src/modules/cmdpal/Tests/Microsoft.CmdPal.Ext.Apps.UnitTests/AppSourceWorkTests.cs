// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.Apps.Catalog;
using Microsoft.CmdPal.Ext.Apps.Catalog.Sources;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Apps.UnitTests;

[TestClass]
public partial class AppSourceWorkTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task SourceWork_OnlyOwnedBackgroundThreadsRunAtReducedPriority(bool background)
    {
        var state = await AppSourceWork.Run(
            () => new WorkerState(
                Thread.CurrentThread.IsThreadPoolThread,
                Thread.CurrentThread.IsBackground,
                GetThreadPriority(GetCurrentThread()),
                Thread.CurrentThread.GetApartmentState()),
            background,
            CancellationToken.None);
        Assert.AreEqual(!background, state.IsPoolThread);
        Assert.IsTrue(state.IsBackground);
        if (background)
        {
            Assert.IsTrue(state.Priority < 0);
            Assert.AreEqual(ApartmentState.MTA, state.Apartment);
        }
        else
        {
            Assert.AreEqual(0, state.Priority, "Ordinary thread-pool workers must retain their normal priority.");
        }
    }

    [TestMethod]
    public async Task SourceWork_BackgroundFailureReachesTheCaller()
    {
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => AppSourceWork.Run<int>(
            () => throw new InvalidOperationException("Fixture failure"),
            background: true,
            CancellationToken.None));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task SourceWork_CanceledWorkDoesNotStart(bool background)
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var ran = false;
        await Assert.ThrowsExactlyAsync<TaskCanceledException>(() => AppSourceWork.Run(
            () => ran = true,
            background,
            cancellation.Token));
        Assert.IsFalse(ran);
    }

    [LibraryImport("kernel32.dll")]
    private static partial nint GetCurrentThread();

    [LibraryImport("kernel32.dll")]
    private static partial int GetThreadPriority(nint thread);

    private sealed record WorkerState(bool IsPoolThread, bool IsBackground, int Priority, ApartmentState Apartment);
}
