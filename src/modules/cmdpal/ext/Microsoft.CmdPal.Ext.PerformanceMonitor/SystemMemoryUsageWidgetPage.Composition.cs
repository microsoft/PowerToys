// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using CoreWidgetProvider.Helpers;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.PerformanceMonitor;

internal sealed partial class SystemMemoryUsageWidgetPage
{
    private static readonly GraphValueScale[] MemoryScales =
    [
        new() { Divisor = 1, Suffix = " B" },
        new() { Divisor = 1024, Suffix = " KB" },
        new() { Divisor = 1024 * 1024, Suffix = " MB" },
        new() { Divisor = 1024d * 1024 * 1024, Suffix = " GB" },
        new() { Divisor = 1024d * 1024 * 1024 * 1024, Suffix = " TB" },
    ];

    private ResourceBarContent _compositionGraph = CreateCompositionGraph(false, false);
    private (bool Installed, bool Detailed) _compositionMode;
    private bool _compositionChanged;

    protected override IContent[] GetGraphContent() => [UsageGraph!, _compositionGraph];

    protected override void OnContentUpdated()
    {
        bool changed;
        lock (ContentData)
        {
            changed = _compositionChanged;
            _compositionChanged = false;
        }

        if (changed)
        {
            RaiseItemsChanged();
        }
    }

    private static ResourceBarContent CreateCompositionGraph(bool installed, bool detailed)
    {
        var series = new List<GraphSeriesInfo>(5);
        if (installed)
        {
            series.Add(new() { Name = Resources.GetResource("Memory_Composition_HardwareReserved"), Color = ColorHelpers.FromRgb(144, 144, 152) });
        }

        if (detailed)
        {
            series.Add(new() { Name = Resources.GetResource("Memory_Composition_InUse"), Color = ColorHelpers.FromRgb(92, 158, 250) });
            series.Add(new() { Name = Resources.GetResource("Memory_Composition_Modified"), Color = ColorHelpers.FromRgb(244, 167, 91) });
            series.Add(new() { Name = Resources.GetResource("Memory_Composition_Standby"), Color = ColorHelpers.FromRgb(86, 185, 165) });
            series.Add(new() { Name = Resources.GetResource("Memory_Composition_Free"), Color = ColorHelpers.FromRgb(174, 216, 245) });
        }
        else
        {
            series.Add(new() { Name = Resources.GetResource("Memory_Widget_Template/UsedMemory"), Color = ColorHelpers.FromRgb(92, 158, 250) });
            series.Add(new() { Name = Resources.GetResource("Memory_Widget_Template/AvailableMemory"), Color = ColorHelpers.FromRgb(174, 216, 245) });
        }

        return new ResourceBarContent(series.ToArray(), MemoryScales)
        {
            DisplayName = Resources.GetResource("Memory_Composition_Title"),
        };
    }

    private void UpdateComposition(MemoryStats stats)
    {
        var mode = (Installed: stats.InstalledMem.HasValue, Detailed: stats.ModifiedMem.HasValue && stats.FreeMem.HasValue);
        var graph = mode == _compositionMode ? _compositionGraph : CreateCompositionGraph(mode.Installed, mode.Detailed);
        var values = new List<double>(5);
        if (mode.Installed)
        {
            values.Add(stats.InstalledMem!.Value - stats.AllMem);
        }

        if (mode.Detailed)
        {
            values.Add(stats.UsedMem - stats.ModifiedMem!.Value);
            values.Add(stats.ModifiedMem.Value);
            values.Add(stats.AvailableMem - stats.FreeMem!.Value);
            values.Add(stats.FreeMem.Value);
        }
        else
        {
            values.Add(stats.UsedMem);
            values.Add(stats.AvailableMem);
        }

        graph.SetSnapshot(values.ToArray());
        if (!ReferenceEquals(graph, _compositionGraph))
        {
            _compositionGraph = graph;
            _compositionMode = mode;
            _compositionChanged = true;
        }
    }

    private void ClearComposition() => _compositionGraph.SetSnapshot(new double[_compositionGraph.GetSeries().Length]);
}
