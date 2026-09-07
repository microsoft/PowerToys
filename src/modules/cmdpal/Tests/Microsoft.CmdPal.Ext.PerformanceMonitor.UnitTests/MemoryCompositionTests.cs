// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Linq;
using CoreWidgetProvider.Helpers;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.Foundation;

namespace Microsoft.CmdPal.Ext.PerformanceMonitor.UnitTests;

[TestClass]
public partial class MemoryCompositionTests
{
    private const ulong Gigabyte = 1024UL * 1024 * 1024;

    private static readonly string[] CompositionKeys = ["HardwareReserved", "InUse", "Modified", "Standby", "Free"];

    [TestMethod]
    public void Composition_PartitionsInstalledMemoryWithoutCountingModifiedPagesTwice()
    {
        using var stats = new MemoryStats();
        using var page = new SystemMemoryUsageWidgetPage(stats);
        stats.ApplyPhysicalMemory(31 * Gigabyte, 8 * Gigabyte, 32 * Gigabyte, Gigabyte, 2 * Gigabyte);
        page.UpdateWidget();

        var graph = (IResourceBarContent)page.GetContent()[1];
        CollectionAssert.AreEqual(
            CompositionKeys.Select(name => Resources.GetResource("Memory_Composition_" + name)).ToArray(),
            graph.GetSeries().Select(series => series.Name).ToArray());
        AssertValues(graph, 1, 22, 1, 6, 2);
        Assert.AreEqual(32d * Gigabyte, graph.GetSnapshot().Sum());
        Assert.AreEqual(7 * Gigabyte, stats.MemCached);

        // The existing history includes modified pages in the unavailable memory.
        Assert.AreEqual(23d, stats.MemoryHistory.GetSnapshot()[1].Value);
        var scales = graph.GetValueScales().Select(scale => new GraphValueScale
        {
            Divisor = scale.Divisor,
            Suffix = scale.Suffix,
        }).ToArray();
        Assert.AreEqual(22d.ToString("0.0", CultureInfo.CurrentCulture) + " GB", GraphValueFormatter.Format(22d * Gigabyte, graph.ValueFormat, graph.ValueSuffix, scales));
        Assert.AreEqual(256d.ToString("0.0", CultureInfo.CurrentCulture) + " MB", GraphValueFormatter.Format(Gigabyte / 4d, graph.ValueFormat, graph.ValueSuffix, scales));
    }

    [TestMethod]
    public void Composition_ReusesTheBarAndReflectsCapacityChanges()
    {
        using var stats = new MemoryStats();
        using var page = new SystemMemoryUsageWidgetPage(stats);
        stats.ApplyPhysicalMemory(31 * Gigabyte, 8 * Gigabyte, 32 * Gigabyte, Gigabyte, 2 * Gigabyte);
        page.UpdateWidget();
        var content = page.GetContent();

        stats.ApplyPhysicalMemory(63 * Gigabyte, 20 * Gigabyte, 64 * Gigabyte, 3 * Gigabyte, 4 * Gigabyte);
        page.UpdateWidget();
        var updated = page.GetContent();

        Assert.AreSame(content[0], updated[0]);
        Assert.AreSame(content[1], updated[1]);
        Assert.AreSame(content[2], updated[2]);
        AssertValues((IResourceBarContent)updated[1], 1, 40, 3, 16, 4);
    }

    [TestMethod]
    public void Composition_BoundsSeparatelyReadPageListsToTheirPartitions()
    {
        using var stats = new MemoryStats();
        using var page = new SystemMemoryUsageWidgetPage(stats);
        stats.ApplyPhysicalMemory(32 * Gigabyte, 8 * Gigabyte, 33 * Gigabyte, 30 * Gigabyte, 10 * Gigabyte);
        page.UpdateWidget();

        AssertValues((IResourceBarContent)page.GetContent()[1], 1, 0, 24, 0, 8);
        Assert.AreEqual(24 * Gigabyte, stats.MemCached);
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    public void MissingPageLists_FallBackToUsedAndAvailable(bool modifiedAvailable, bool freeAvailable)
    {
        using var stats = new MemoryStats();
        using var page = new SystemMemoryUsageWidgetPage(stats);
        stats.ApplyPhysicalMemory(
            31 * Gigabyte,
            8 * Gigabyte,
            32 * Gigabyte,
            modifiedAvailable ? Gigabyte : null,
            freeAvailable ? 2 * Gigabyte : null);
        page.UpdateWidget();

        var graph = (IResourceBarContent)page.GetContent()[1];
        AssertValues(graph, 1, 23, 8);
        Assert.AreEqual(Resources.GetResource("Memory_Widget_Template/UsedMemory"), graph.GetSeries()[1].Name);
        Assert.AreEqual(Resources.GetResource("Memory_Widget_Template/AvailableMemory"), graph.GetSeries()[2].Name);
        Assert.IsNull(stats.ModifiedMem);
        Assert.IsNull(stats.FreeMem);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(16)]
    public void UnknownOrInvalidInstalledMemory_OmitsHardwareReserved(int installedGigabytes)
    {
        using var stats = new MemoryStats();
        using var page = new SystemMemoryUsageWidgetPage(stats);
        stats.ApplyPhysicalMemory(32 * Gigabyte, 8 * Gigabyte, installedGigabytes == 0 ? null : (ulong)installedGigabytes * Gigabyte, Gigabyte, 2 * Gigabyte);
        page.UpdateWidget();

        var graph = (IResourceBarContent)page.GetContent()[1];
        AssertValues(graph, 23, 1, 6, 2);
        Assert.AreEqual(Resources.GetResource("Memory_Composition_InUse"), graph.GetSeries()[0].Name);
        Assert.IsNull(stats.InstalledMem);
    }

    [TestMethod]
    public void AvailabilityChanges_ReplaceOnlyTheBarAndPublishBeforeNotifying()
    {
        using var stats = new MemoryStats();
        using var page = new SystemMemoryUsageWidgetPage(stats);
        var original = page.GetContent();
        var notifications = 0;
        IResourceBarContent published = null;
        TypedEventHandler<object, IItemsChangedEventArgs> handler = (_, _) =>
        {
            notifications++;
            published = (IResourceBarContent)page.GetContent()[1];
        };
        page.ItemsChanged += handler;
        page.PopActivate(); // Keep this test independent of hardware sampling.
        try
        {
            stats.ApplyPhysicalMemory(31 * Gigabyte, 8 * Gigabyte, 32 * Gigabyte, Gigabyte, 2 * Gigabyte);
            page.UpdateWidget();
            Assert.AreEqual(1, notifications);
            Assert.IsNotNull(published);
            AssertValues(published, 1, 22, 1, 6, 2);
            var detailed = page.GetContent();
            Assert.AreNotSame(original[1], detailed[1]);
            Assert.AreSame(original[0], detailed[0]);
            Assert.AreSame(original[2], detailed[2]);

            stats.ApplyPhysicalMemory(31 * Gigabyte, 9 * Gigabyte, 32 * Gigabyte, Gigabyte, 2 * Gigabyte);
            page.UpdateWidget();
            Assert.AreEqual(1, notifications);
            Assert.AreSame(detailed[1], page.GetContent()[1]);

            stats.ApplyPhysicalMemory(31 * Gigabyte, 10 * Gigabyte, 32 * Gigabyte);
            page.UpdateWidget();
            Assert.AreEqual(2, notifications);
            Assert.AreNotSame(detailed[1], published);
            AssertValues(published, 1, 21, 10);
            AssertValues((IResourceBarContent)detailed[1], 1, 21, 1, 7, 2);
        }
        finally
        {
            page.ItemsChanged -= handler;
        }
    }

    private static void AssertValues(IResourceBarContent graph, params double[] gigabytes)
        => CollectionAssert.AreEqual(gigabytes.Select(value => value * Gigabyte).ToArray(), graph.GetSnapshot());
}
