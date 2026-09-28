// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using AdvancedPaste.Cli;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AdvancedPaste.Cli.UnitTests;

[TestClass]
public class SystemClipboardAdapterTests
{
    [TestMethod]
    public void FlushClipboard_RetriesTransientFailures()
    {
        var attempts = 0;

        SystemClipboardAdapter.FlushClipboard(() =>
        {
            if (++attempts < 3)
            {
                throw new InvalidOperationException("Transient flush failure.");
            }
        });

        Assert.AreEqual(3, attempts);
    }

    [TestMethod]
    public void FlushClipboard_ThrowsAfterFinalFailure()
    {
        var attempts = 0;

        var exception = Assert.ThrowsExactly<InvalidOperationException>(() =>
            SystemClipboardAdapter.FlushClipboard(() =>
            {
                attempts++;
                throw new InvalidOperationException("Persistent flush failure.");
            }));

        Assert.AreEqual(5, attempts);
        Assert.AreEqual("Persistent flush failure.", exception.Message);
    }

    [TestMethod]
    public async Task RunOnSta_FromMta_ExecutesOnSta()
    {
        var apartment = await Task.Run(() => SystemClipboardAdapter.RunOnSta(() => Thread.CurrentThread.GetApartmentState()));

        Assert.AreEqual(ApartmentState.STA, apartment);
    }

    [TestMethod]
    public async Task RunOnSta_PropagatesOriginalException()
    {
        var exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            Task.Run(() => SystemClipboardAdapter.RunOnSta<int>(() => throw new InvalidOperationException("Test failure"))));

        Assert.AreEqual("Test failure", exception.Message);
    }
}
