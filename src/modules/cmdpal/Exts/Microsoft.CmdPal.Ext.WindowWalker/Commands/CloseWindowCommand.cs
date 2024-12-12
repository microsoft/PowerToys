// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.WindowWalker.Components;
using Microsoft.CmdPal.Ext.WindowWalker.Properties;
using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.Extensions.Helpers;

namespace Microsoft.CmdPal.Ext.WindowWalker.Commands;

internal sealed partial class CloseWindowCommand : InvokableCommand
{
    private readonly Window _window;

    public CloseWindowCommand(Window window)
    {
        Icon = new("\xE8BB");
        Name = $"{Resources.windowwalker_Close}";
        _window = window;
    }

    public override ICommandResult Invoke()
    {
        if (!_window.IsWindow)
        {
            ExtensionHost.LogMessage(new LogMessage() { Message = $"Can not close the window '{_window.Title}' ({_window.Hwnd}), because it doesn't exist." });
        }

        _window.CloseThisWindow();
        return CommandResult.Dismiss();
    }
}
