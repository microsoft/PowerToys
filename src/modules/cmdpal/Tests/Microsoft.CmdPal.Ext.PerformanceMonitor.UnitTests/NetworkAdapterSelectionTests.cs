// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CoreWidgetProvider.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.PerformanceMonitor.UnitTests;

[TestClass]
public sealed class NetworkAdapterSelectionTests
{
    [TestMethod]
    public void SelectBusiestNetworkIndex_PrefersCurrentTraffic()
    {
        NetworkStats.Data[] samples =
        [
            new() { Bandwidth = 10_000_000_000 },
            new() { Bandwidth = 1_000_000_000, Sent = 100, Received = 200 },
        ];

        Assert.AreEqual(1, NetworkStats.SelectBusiestNetworkIndex(samples, 0));
    }

    [TestMethod]
    public void SelectBusiestNetworkIndex_SelectsConnectedAdapterWhenFallbackIsDisconnected()
    {
        NetworkStats.Data[] samples =
        [
            new(),
            new() { Bandwidth = 500_000_000 },
            new() { Bandwidth = 1_000_000_000 },
        ];

        Assert.AreEqual(1, NetworkStats.SelectBusiestNetworkIndex(samples, 0));
    }

    [TestMethod]
    public void SelectBusiestNetworkIndex_PreservesConnectedFallbackWhenIdle()
    {
        NetworkStats.Data[] samples =
        [
            new() { Bandwidth = 10_000_000_000 },
            new() { Bandwidth = 1_000_000_000 },
        ];

        Assert.AreEqual(1, NetworkStats.SelectBusiestNetworkIndex(samples, 1));
    }

    [TestMethod]
    public void SelectBusiestNetworkIndex_RequiresMeaningfulThroughputLead()
    {
        NetworkStats.Data[] samples =
        [
            new() { Bandwidth = 1_000_000_000, Sent = 500, Received = 500 },
            new() { Bandwidth = 1_000_000_000, Sent = 550, Received = 550 },
        ];

        Assert.AreEqual(0, NetworkStats.SelectBusiestNetworkIndex(samples, 0));

        samples[1].Received = 1_450;
        Assert.AreEqual(1, NetworkStats.SelectBusiestNetworkIndex(samples, 0));
    }

    [TestMethod]
    public void SelectBusiestNetworkIndex_PreservesFallbackWhenSamplesAreEqual()
    {
        NetworkStats.Data[] samples = [new(), new(), new()];

        Assert.AreEqual(2, NetworkStats.SelectBusiestNetworkIndex(samples, 2));
    }

    [TestMethod]
    public void SelectAdjacentNetworkIndex_SkipsDisconnectedAdaptersAndWraps()
    {
        NetworkStats.Data[] samples =
        [
            new() { Bandwidth = 1_000_000_000 },
            new(),
            new() { Bandwidth = 500_000_000 },
            new(),
        ];

        Assert.AreEqual(2, NetworkStats.CountSelectableNetworks(samples));
        Assert.AreEqual(2, NetworkStats.SelectNextNetworkIndex(samples, 0));
        Assert.AreEqual(0, NetworkStats.SelectNextNetworkIndex(samples, 2));
        Assert.AreEqual(2, NetworkStats.SelectPreviousNetworkIndex(samples, 0));
        Assert.AreEqual(0, NetworkStats.SelectPreviousNetworkIndex(samples, 2));
    }

    [TestMethod]
    public void SelectAdjacentNetworkIndex_PreservesSelectionWhenAllAdaptersAreDisconnected()
    {
        NetworkStats.Data[] samples = [new(), new()];

        Assert.AreEqual(0, NetworkStats.CountSelectableNetworks(samples));
        Assert.AreEqual(1, NetworkStats.SelectPreviousNetworkIndex(samples, 1));
        Assert.AreEqual(1, NetworkStats.SelectNextNetworkIndex(samples, 1));
    }

    [TestMethod]
    public void ManualSelection_PreventsAutomaticSelectionFromOverwritingIndex()
    {
        var selection = new PerformanceMetricSelectionState();

        Assert.AreEqual(2, selection.UpdateAutomaticNetworkIndex(2));
        Assert.IsTrue(selection.SelectNetworkManually(1));

        Assert.IsFalse(selection.IsNetworkSelectionAutomatic);
        Assert.AreEqual(1, selection.UpdateAutomaticNetworkIndex(0));
        Assert.AreEqual(1, selection.NetworkIndex);
    }

    [TestMethod]
    public void NoOpManualSelection_PreservesAutomaticSelection()
    {
        var selection = new PerformanceMetricSelectionState();

        Assert.AreEqual(1, selection.UpdateAutomaticNetworkIndex(1));
        Assert.IsFalse(selection.SelectNetworkManually(1));

        Assert.IsTrue(selection.IsNetworkSelectionAutomatic);
        Assert.AreEqual(0, selection.UpdateAutomaticNetworkIndex(0));
    }

    [TestMethod]
    public void DisconnectedManualSelection_RecoversAndRestoresAutomaticSelection()
    {
        var selection = new PerformanceMetricSelectionState();

        Assert.AreEqual(1, selection.UpdateAutomaticNetworkIndex(1));
        Assert.IsTrue(selection.SelectNetworkManually(2));
        Assert.AreEqual(1, selection.RecoverNetworkSelection(2, 1));

        Assert.IsTrue(selection.IsNetworkSelectionAutomatic);
        Assert.AreEqual(0, selection.UpdateAutomaticNetworkIndex(0));
    }

    [TestMethod]
    public void Recovery_DoesNotOverwriteNewerManualSelection()
    {
        var selection = new PerformanceMetricSelectionState();

        Assert.AreEqual(0, selection.UpdateAutomaticNetworkIndex(0));
        Assert.IsTrue(selection.SelectNetworkManually(1));
        Assert.IsTrue(selection.SelectNetworkManually(2));
        Assert.AreEqual(2, selection.RecoverNetworkSelection(1, 0));

        Assert.IsFalse(selection.IsNetworkSelectionAutomatic);
        Assert.AreEqual(2, selection.NetworkIndex);
    }
}
