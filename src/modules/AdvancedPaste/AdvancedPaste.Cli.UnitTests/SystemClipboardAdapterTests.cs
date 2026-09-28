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
