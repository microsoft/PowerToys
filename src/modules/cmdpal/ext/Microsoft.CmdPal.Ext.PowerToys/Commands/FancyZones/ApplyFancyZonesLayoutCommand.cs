// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerToysExtension.Helpers;

namespace PowerToysExtension.Commands;

internal sealed partial class ApplyFancyZonesLayoutCommand : InvokableCommand
{
    private readonly FancyZonesLayoutDescriptor _layout;
    private readonly int? _targetMonitorIndex;

    public ApplyFancyZonesLayoutCommand(FancyZonesLayoutDescriptor layout, int? targetMonitorIndex)
    {
        _layout = layout;
        _targetMonitorIndex = targetMonitorIndex;

        Name = targetMonitorIndex is null
            ? $"Apply '{layout.Title}' to all monitors"
            : $"Apply '{layout.Title}' to Monitor {targetMonitorIndex.Value}";
    }

    public override CommandResult Invoke()
    {
        var result = _targetMonitorIndex is null
            ? FancyZonesDataService.ApplyLayoutToAllMonitors(_layout)
            : FancyZonesDataService.ApplyLayoutToMonitorIndex(_layout, _targetMonitorIndex.Value);

        return result.Success
            ? CommandResult.Dismiss()
            : CommandResult.ShowToast(result.Message);
    }
}
