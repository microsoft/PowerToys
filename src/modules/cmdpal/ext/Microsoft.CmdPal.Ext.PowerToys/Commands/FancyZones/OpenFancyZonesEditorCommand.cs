// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerToysExtension.Helpers;

namespace PowerToysExtension.Commands;

/// <summary>
/// Launches the FancyZones layout editor via the shared event.
/// </summary>
internal sealed partial class OpenFancyZonesEditorCommand : InvokableCommand
{
    public OpenFancyZonesEditorCommand()
    {
        Name = "Open FancyZones Editor";
    }

    public override CommandResult Invoke()
    {
        try
        {
            using var evt = new EventWaitHandle(false, EventResetMode.AutoReset, PowerToysEventNames.FancyZonesToggleEditor);
            evt.Set();
            return CommandResult.Dismiss();
        }
        catch (Exception ex)
        {
            return CommandResult.ShowToast($"Failed to open FancyZones editor: {ex.Message}");
        }
    }
}
