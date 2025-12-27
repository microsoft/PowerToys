// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerToys.Interop;

namespace PowerToysExtension.Commands;

/// <summary>
/// Toggles Cursor Wrap via the shared trigger event.
/// </summary>
internal sealed partial class ToggleCursorWrapCommand : InvokableCommand
{
    public ToggleCursorWrapCommand()
    {
        Name = "Toggle Cursor Wrap";
    }

    public override CommandResult Invoke()
    {
        try
        {
            using var evt = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.CursorWrapTriggerEvent());
            evt.Set();
            return CommandResult.Dismiss();
        }
        catch (Exception ex)
        {
            return CommandResult.ShowToast($"Failed to toggle Cursor Wrap: {ex.Message}");
        }
    }
}
