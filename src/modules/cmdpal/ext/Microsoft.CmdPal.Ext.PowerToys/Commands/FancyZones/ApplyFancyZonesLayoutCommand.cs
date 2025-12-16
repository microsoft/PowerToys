// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerToysExtension.Helpers;

namespace PowerToysExtension.Commands;

internal sealed partial class ApplyFancyZonesLayoutCommand : InvokableCommand
{
    private readonly FancyZonesLayoutDescriptor _layout;
    private readonly FancyZonesMonitorDescriptor? _targetMonitor;

    public ApplyFancyZonesLayoutCommand(FancyZonesLayoutDescriptor layout, FancyZonesMonitorDescriptor? monitor)
    {
        _layout = layout;
        _targetMonitor = monitor;

        Name = monitor is null ? "Apply to all monitors" : $"Apply to Monitor {monitor.Value.Title}";

        Icon = new IconInfo("\uF78C");
    }

    public override CommandResult Invoke()
    {
        var monitor = _targetMonitor;
        var (success, message) = monitor is null
            ? FancyZonesDataService.ApplyLayoutToAllMonitors(_layout)
            : FancyZonesDataService.ApplyLayoutToMonitor(_layout, monitor.Value);

        return success
            ? CommandResult.Dismiss()
            : CommandResult.ShowToast(message);
    }
}
