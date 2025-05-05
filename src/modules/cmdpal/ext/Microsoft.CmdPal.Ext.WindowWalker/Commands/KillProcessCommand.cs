// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.WindowWalker.Components;
using Microsoft.CmdPal.Ext.WindowWalker.Helpers;
using Microsoft.CmdPal.Ext.WindowWalker.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.WindowWalker.Commands;

internal sealed partial class KillProcessCommand : InvokableCommand
{
    private readonly Window _window;

    public KillProcessCommand(Window window)
    {
        Icon = new IconInfo("\xE74D"); // Delete symbol
        Name = $"{Resources.windowwalker_Kill}";
        _window = window;
    }

    /// <summary>
    /// Method to initiate killing the process of a window
    /// </summary>
    /// <param name="window">Window data</param>
    /// <returns>True if the PT Run window should close, otherwise false.</returns>
    private static bool KillProcess(Window window)
    {
        // Validate process
        if (!window.IsWindow || !window.Process.DoesExist || string.IsNullOrEmpty(window.Process.Name) || !window.Process.Name.Equals(WindowProcess.GetProcessNameFromProcessID(window.Process.ProcessID), StringComparison.Ordinal))
        {
            ExtensionHost.LogMessage(new LogMessage() { Message = $"Cannot kill process '{window.Process.Name}' ({window.Process.ProcessID}) of the window '{window.Title}' ({window.Hwnd}), because it doesn't exist." });

            // TODO GH #86 -- need to figure out how to show status message once implemented on host
            return false;
        }

        // Request user confirmation
        if (SettingsManager.Instance.ConfirmKillProcess)
        {
            // TODO GH #138, #153 -- need to figure out how to confirm kill process? should this just be the same status thing... maybe not? Need message box? Could be nested context menu.
            /*
            string messageBody = $"{Resources.wox_plugin_windowwalker_KillMessage}\n"
                + $"{window.Process.Name} ({window.Process.ProcessID})\n\n"
                + $"{(window.Process.IsUwpApp ? Resources.wox_plugin_windowwalker_KillMessageUwp : Resources.wox_plugin_windowwalker_KillMessageQuestion)}";
            MessageBoxResult messageBoxResult = MessageBox.Show(
                messageBody,
                Resources.wox_plugin_windowwalker_plugin_name + " - " + Resources.wox_plugin_windowwalker_KillMessageTitle,
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (messageBoxResult == MessageBoxResult.No)
            {
                return false;
            }
            */
        }

        // Kill process
        window.Process.KillThisProcess(SettingsManager.Instance.KillProcessTree);
        return !SettingsManager.Instance.OpenAfterKillAndClose;
    }

    public override ICommandResult Invoke()
    {
        if (KillProcess(_window))
        {
            return CommandResult.Dismiss();
        }

        return CommandResult.KeepOpen();
    }
}
