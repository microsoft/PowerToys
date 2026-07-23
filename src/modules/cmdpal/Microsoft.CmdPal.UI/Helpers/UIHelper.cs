// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Windows.System;

namespace Microsoft.CmdPal.UI.Helpers;

public static partial class UIHelper
{
    static UIHelper()
    {
    }

    public static void AnnounceActionForAccessibility(UIElement ue, string announcement, string activityID)
    {
        if (FrameworkElementAutomationPeer.FromElement(ue) is AutomationPeer peer)
        {
            peer.RaiseNotificationEvent(
                AutomationNotificationKind.ActionCompleted,
                AutomationNotificationProcessing.ImportantMostRecent,
                announcement,
                activityID);
        }
    }

    public static string FormatKeyChordForDisplay(KeyChord shortcut)
    {
        if ((VirtualKey)shortcut.Vkey == VirtualKey.None)
        {
            return string.Empty;
        }

        var result = string.Empty;

        if (shortcut.Modifiers.HasFlag(VirtualKeyModifiers.Control))
        {
            result += "Ctrl+";
        }

        if (shortcut.Modifiers.HasFlag(VirtualKeyModifiers.Shift))
        {
            result += "Shift+";
        }

        if (shortcut.Modifiers.HasFlag(VirtualKeyModifiers.Menu))
        {
            result += "Alt+";
        }

        result += (VirtualKey)shortcut.Vkey;

        return result;
    }
}
