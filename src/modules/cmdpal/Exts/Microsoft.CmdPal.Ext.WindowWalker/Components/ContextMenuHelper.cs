// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Windows;
using ABI.Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.Ext.WindowWalker.Commands;
using Microsoft.CmdPal.Ext.WindowWalker.Helpers;
using Microsoft.CmdPal.Ext.WindowWalker.Properties;
using Microsoft.CmdPal.Extensions.Helpers;
using Windows.System;

namespace Microsoft.CmdPal.Ext.WindowWalker.Components;

internal sealed class ContextMenuHelper
{
    internal static List<CommandContextItem> GetContextMenuResults(in WindowWalkerListItem listItem)
    {
        if (!(listItem?.Window is Window windowData))
        {
            return new List<CommandContextItem>(0);
        }

        var contextMenu = new List<CommandContextItem>()
        {
            new(new CloseWindowCommand(windowData))
            {
                RequestedShortcut = new(true, false, false, false, (int)VirtualKey.F4, 0),
            },
        };

        // Hide menu if Explorer.exe is the shell process or the process name is ApplicationFrameHost.exe
        // In the first case we would crash the windows ui and in the second case we would kill the generic process for uwp apps.
        if (!windowData.Process.IsShellProcess && !(windowData.Process.IsUwpApp && string.Equals(windowData.Process.Name, "ApplicationFrameHost.exe", StringComparison.OrdinalIgnoreCase))
            && !(windowData.Process.IsFullAccessDenied && SettingsManager.Instance.HideKillProcessOnElevatedProcesses))
        {
            contextMenu.Add(new CommandContextItem(new KillProcessCommand(windowData))
            {
                RequestedShortcut = new(true, false, false, false, (int)VirtualKey.Delete, 0),
            });
        }

        return contextMenu;
    }
}
