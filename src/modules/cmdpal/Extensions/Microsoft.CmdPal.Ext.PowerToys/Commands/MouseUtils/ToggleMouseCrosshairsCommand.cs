// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerToys.Interop;

namespace PowerToysExtension.Commands;

/// <summary>
/// Toggles Mouse Pointer Crosshairs via the shared event.
/// </summary>
internal sealed partial class ToggleMouseCrosshairsCommand : InvokableCommand
{
    public ToggleMouseCrosshairsCommand()
    {
        Name = "Toggle Mouse Crosshairs";
    }

    public override CommandResult Invoke()
    {
        try
        {
            using var evt = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.MouseCrosshairsTriggerEvent());
            evt.Set();
            return CommandResult.Dismiss();
        }
        catch (Exception ex)
        {
            return CommandResult.ShowToast($"Failed to toggle Mouse Crosshairs: {ex.Message}");
        }
    }
}
