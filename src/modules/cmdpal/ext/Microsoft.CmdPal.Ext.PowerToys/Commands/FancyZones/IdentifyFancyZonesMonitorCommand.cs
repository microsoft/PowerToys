// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerToysExtension.Helpers;

namespace PowerToysExtension.Commands;

internal sealed partial class IdentifyFancyZonesMonitorCommand : InvokableCommand
{
    private readonly FancyZonesMonitorDescriptor _monitor;

    public IdentifyFancyZonesMonitorCommand(FancyZonesMonitorDescriptor monitor)
    {
        _monitor = monitor;
        Name = $"Identify {_monitor.Title}";
        Icon = new IconInfo("\uE773");
    }

    public override CommandResult Invoke()
    {
        if (!FancyZonesDataService.TryGetMonitors(out var monitors, out var error))
        {
            return CommandResult.ShowToast(error);
        }

        var monitor = monitors.FirstOrDefault(m => m.Data.MonitorInstanceId == _monitor.Data.MonitorInstanceId);

        if (monitor == null)
        {
            return CommandResult.ShowToast("Monitor not found.");
        }

        FancyZonesMonitorIdentifier.Show(
            monitor.Data.LeftCoordinate,
            monitor.Data.TopCoordinate,
            monitor.Data.WorkAreaWidth,
            monitor.Data.WorkAreaHeight,
            _monitor.Title,
            durationMs: 1200);

        return CommandResult.KeepOpen();
    }
}
