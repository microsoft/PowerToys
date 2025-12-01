// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerToysExtension.Helpers;

namespace PowerToysExtension.Commands;

internal sealed partial class ToggleScreenRulerCommand : InvokableCommand
{
    public ToggleScreenRulerCommand()
    {
        Name = "Toggle Screen Ruler";
    }

    public override CommandResult Invoke()
    {
        try
        {
            using var evt = new EventWaitHandle(false, EventResetMode.AutoReset, PowerToysEventNames.MeasureToolTrigger);
            evt.Set();
            return CommandResult.Dismiss();
        }
        catch (Exception ex)
        {
            return CommandResult.ShowToast($"Failed to toggle Screen Ruler: {ex.Message}");
        }
    }
}
