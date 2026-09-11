// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.PerformanceMonitor;

internal static class PerformanceMonitorDockItemPresentation
{
    internal const string CpuLabelWidth = "5ch";
    internal const string MemoryLabelWidth = "6ch";
    internal const string NetworkUsageLabelWidth = "6ch";
    internal const string DiskActiveTimeLabelWidth = "8ch";
    internal const string GpuLabelWidth = "12ch";
    internal const string BatteryLabelWidth = "6ch";
    internal const string TransferRateLabelWidth = "10ch";
    internal const string DisabledLabelWidth = "8ch";

    internal static ListItem ConfigureValueLabel(ListItem item, string labelWidth)
    {
        item
            .SetDockLabelWidth(labelWidth)
            .SetDockLabelTabularDigits();

        // item.SetDockLabelTrailingAlignment();
        return item;
    }
}
