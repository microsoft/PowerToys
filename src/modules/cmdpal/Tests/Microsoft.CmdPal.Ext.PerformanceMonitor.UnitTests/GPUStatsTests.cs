// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CoreWidgetProvider.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.PerformanceMonitor.UnitTests;

[TestClass]
public class GPUStatsTests
{
    [TestMethod]
    public void GetGPUDisplayInfo_PreservesTheFullNameAndCachesAShortName()
    {
        const string fullName = "NVIDIA RTX A3000 Laptop GPU";
        using var stats = new GPUStats(new() { [1] = new(fullName, IsSoftware: false) }, [1]);

        var info = stats.GetGPUDisplayInfo(0);

        Assert.AreEqual(fullName, info.Name);
        Assert.AreEqual("RTX A3000", info.ShortName);
        Assert.AreEqual(1, info.AdapterCount);
        Assert.AreSame(info.ShortName, stats.GetGPUDisplayInfo(0).ShortName);
    }

    [TestMethod]
    public void GetGPUDisplayInfo_CountsHardwareWithoutActiveCounters()
    {
        using var stats = new GPUStats(
            new()
            {
                [1] = new("NVIDIA RTX A3000 Laptop GPU", IsSoftware: false),
                [2] = new("Intel(R) Iris(R) Xe Graphics", IsSoftware: false),
                [3] = new("Microsoft Basic Render Driver", IsSoftware: true),
            },
            [1]);

        var info = stats.GetGPUDisplayInfo(0);

        Assert.AreEqual(2, info.AdapterCount);
        Assert.AreEqual("RTX A3000", info.ShortName);
        Assert.AreEqual(0, stats.GetNextGPUIndex(0));
    }

    [TestMethod]
    public void GetGPUDisplayInfo_SoftwareRendererDoesNotTurnASingleGpuIntoMultipleGpus()
    {
        using var stats = new GPUStats(
            new()
            {
                [1] = new("NVIDIA RTX A3000 Laptop GPU", IsSoftware: false),
                [2] = new("Microsoft Basic Render Driver", IsSoftware: true),
            },
            [2, 1]);

        Assert.AreEqual(1, stats.GetGPUDisplayInfo(0).AdapterCount);
        Assert.AreEqual("RTX A3000", stats.GetGPUDisplayInfo(0).ShortName);
        Assert.AreEqual(0, stats.GetNextGPUIndex(0));
    }

    [TestMethod]
    public void GetGPUDisplayInfo_SoftwareOnlySystemHasOneDisplayableAdapter()
    {
        using var stats = new GPUStats(new() { [1] = new("Microsoft Basic Render Driver", IsSoftware: true) }, [1]);

        Assert.AreEqual(1, stats.GetGPUDisplayInfo(0).AdapterCount);
        Assert.AreEqual("Microsoft Basic Render Driver", stats.GetGPUDisplayInfo(0).Name);
    }

    [TestMethod]
    public void GetGPUDisplayInfo_MissingDxgiNamesUseDiscoveredAdaptersAndIndexedFallbacks()
    {
        using var stats = new GPUStats([], [1, 2]);

        Assert.AreEqual(2, stats.GetGPUDisplayInfo(0).AdapterCount);
        Assert.AreEqual("GPU 0", stats.GetGPUDisplayInfo(0).ShortName);
        Assert.AreEqual("GPU 1", stats.GetGPUDisplayInfo(1).ShortName);
    }

    [TestMethod]
    public void GetGPUDisplayInfo_DisambiguatesNamesThatBecomeIdenticalAfterShortening()
    {
        const string laptop = "NVIDIA GeForce RTX 4070 Laptop GPU";
        const string desktop = "NVIDIA GeForce RTX 4070";
        using var stats = new GPUStats(
            new()
            {
                [1] = new(laptop, IsSoftware: false),
                [2] = new(desktop, IsSoftware: false),
            },
            [1, 2]);

        Assert.AreEqual("RTX 4070 (0)", stats.GetGPUDisplayInfo(0).ShortName);
        Assert.AreEqual("RTX 4070 (1)", stats.GetGPUDisplayInfo(1).ShortName);
        Assert.AreEqual(laptop, stats.GetGPUDisplayInfo(0).Name);
        Assert.AreEqual(desktop, stats.GetGPUDisplayInfo(1).Name);
    }

    [TestMethod]
    [DataRow(-1)]
    [DataRow(1)]
    public void GetGPUDisplayInfo_InvalidIndexHasNoModelName(int index)
    {
        using var stats = new GPUStats(new() { [1] = new("NVIDIA RTX A3000 Laptop GPU", IsSoftware: false) }, [1]);

        var info = stats.GetGPUDisplayInfo(index);

        Assert.AreEqual(string.Empty, info.Name);
        Assert.AreEqual(string.Empty, info.ShortName);
        Assert.AreEqual(1, info.AdapterCount);
    }
}
