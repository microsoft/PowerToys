// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.PerformanceMonitor;

internal static class PerformanceMonitorDockItemPresentation
{
    internal const string DisabledLabelWidth = "8ch";
    internal const string PercentageTitleWidth = "4.6ch";

    internal const string GpuSubtitleWidth = "12ch";
    internal const string TransferRateLabelWidth = "10ch";

    internal static ListItem ConfigureValueLabel(ListItem item, string titleWidth, string? subtitleWidth = null)
    {
        item
            .SetDockLabelWidths(titleWidth, subtitleWidth ?? titleWidth)
            .SetDockLabelWidthSamples(subtitleSample: subtitleWidth is null ? item.Subtitle : null)
            .SetDockLabelTabularDigits();

        // item.SetDockLabelTrailingAlignment();
        return item;
    }
}
