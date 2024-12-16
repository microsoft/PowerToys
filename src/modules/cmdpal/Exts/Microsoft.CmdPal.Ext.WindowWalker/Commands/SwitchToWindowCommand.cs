// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using Microsoft.CmdPal.Ext.WindowWalker.Components;
using Microsoft.CmdPal.Ext.WindowWalker.Properties;
using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.Extensions.Helpers;

namespace Microsoft.CmdPal.Ext.WindowWalker.Commands;

internal sealed partial class SwitchToWindowCommand : InvokableCommand
{
    private readonly Window? _window;

    public SwitchToWindowCommand(Window? window)
    {
        Name = Resources.switch_to_command_title;
        Icon = new(string.Empty);
        _window = window;
        if (_window != null)
        {
            var p = Process.GetProcessById((int)_window.Process.ProcessID);
            if (p != null)
            {
                try
                {
                    Icon = new(p.MainModule?.FileName);
                }
                catch
                {
                }
            }
        }
    }

    public override ICommandResult Invoke()
    {
        if (_window is null)
        {
            ExtensionHost.LogMessage(new LogMessage() { Message = "Can not switch to the window, because it doesn't exist." });
            return CommandResult.Dismiss();
        }

        _window.SwitchToWindow();

        return CommandResult.Dismiss();
    }
}
