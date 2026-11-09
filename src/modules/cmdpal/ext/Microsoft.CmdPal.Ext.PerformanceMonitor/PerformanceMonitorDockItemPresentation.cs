// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CoreWidgetProvider.Helpers;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.PerformanceMonitor;

internal static class PerformanceMonitorDockItemPresentation
{
    internal static readonly DockLabelWidth DisabledLabelWidth = DockLabelWidth.Characters(8);
    internal static readonly DockLabelWidth PercentageTitleWidth = DockLabelWidth.Sample("100%");
    internal static readonly DockLabelWidth GpuSubtitleWidth = DockLabelWidth.Characters(12);
    internal static readonly DockLabelWidth TransferRateLabelWidth = DockLabelWidth.Characters(10);

    internal static ListItem ConfigureGpuValueLabel(ListItem item, GPUStats.DisplayInfo? gpu)
    {
        var hasMultipleAdapters = gpu is { AdapterCount: > 1 };
        var modelName = hasMultipleAdapters ? gpu!.ShortName : string.Empty;
        var hasModelName = !string.IsNullOrEmpty(modelName);
        item.Subtitle = hasModelName ? modelName : Resources.GetResource("GPU_Usage_Subtitle");
        return ConfigureValueLabel(item, PercentageTitleWidth, hasMultipleAdapters ? GpuSubtitleWidth : null);
    }

    internal static ListItem ConfigureValueLabel(ListItem item, DockLabelWidth titleWidth, DockLabelWidth? subtitleWidth = null)
    {
        item
            .SetDockLabelReservations(titleWidth, subtitleWidth ?? DockLabelWidth.Sample(item.Subtitle))
            .SetDockLabelTabularDigits();

        // item.SetDockLabelTrailingAlignment();
        return item;
    }
}
