// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerToysExtension.Helpers;

namespace PowerToysExtension.Commands;

internal sealed partial class IdentifyFancyZonesMonitorCommand : InvokableCommand
{
    private readonly int _monitorIndex;

    public IdentifyFancyZonesMonitorCommand(int monitorIndex)
    {
        _monitorIndex = monitorIndex;
        Name = $"Identify Monitor {_monitorIndex}";
        Icon = new IconInfo("\uE7F4");
    }

    public override CommandResult Invoke()
    {
        if (!FancyZonesDataService.TryGetMonitors(out var monitors, out var error))
        {
            return CommandResult.ShowToast(error);
        }

        if (_monitorIndex < 1 || _monitorIndex > monitors.Count)
        {
            return CommandResult.ShowToast($"Monitor {_monitorIndex} not found.");
        }

        var monitor = monitors[_monitorIndex - 1].Data;
        FancyZonesMonitorIdentifier.Show(
            monitor.LeftCoordinate,
            monitor.TopCoordinate,
            monitor.WorkAreaWidth,
            monitor.WorkAreaHeight,
            $"Monitor {_monitorIndex}",
            durationMs: 1200);

        return CommandResult.KeepOpen();
    }
}
