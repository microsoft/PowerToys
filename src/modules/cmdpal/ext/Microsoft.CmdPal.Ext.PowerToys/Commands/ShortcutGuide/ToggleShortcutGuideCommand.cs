// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerToysExtension.Helpers;

namespace PowerToysExtension.Commands;

/// <summary>
/// Toggles the Shortcut Guide UI via the shared trigger event.
/// </summary>
internal sealed partial class ToggleShortcutGuideCommand : InvokableCommand
{
    public ToggleShortcutGuideCommand()
    {
        Name = "Toggle Shortcut Guide";
    }

    public override CommandResult Invoke()
    {
        try
        {
            using var evt = new EventWaitHandle(false, EventResetMode.AutoReset, PowerToysEventNames.ShortcutGuideTrigger);
            evt.Set();
            return CommandResult.Dismiss();
        }
        catch (Exception ex)
        {
            return CommandResult.ShowToast($"Failed to toggle Shortcut Guide: {ex.Message}");
        }
    }
}
