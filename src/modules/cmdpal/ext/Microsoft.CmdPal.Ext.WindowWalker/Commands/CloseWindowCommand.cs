// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.Ext.WindowWalker.Components;
using Microsoft.CmdPal.Ext.WindowWalker.Helpers;
using Microsoft.CmdPal.Ext.WindowWalker.Messages;
using Microsoft.CmdPal.Ext.WindowWalker.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.WindowWalker.Commands;

internal sealed partial class CloseWindowCommand : InvokableCommand
{
    private readonly Window _window;

    public CloseWindowCommand(Window window)
    {
        Icon = Icons.CloseWindow;
        Name = $"{Resources.windowwalker_Close}";
        _window = window;
    }

    public override ICommandResult Invoke()
    {
        if (!_window.IsWindow)
        {
            ExtensionHost.LogMessage(new LogMessage() { Message = $"Cannot close the window '{_window.Title}' ({_window.Hwnd}), because it doesn't exist." });
        }

        _window.CloseThisWindow();

        if (SettingsManager.Instance.OpenAfterKillAndClose)
        {
            // Send message to refresh window list after closing
            WeakReferenceMessenger.Default.Send(new RefreshWindowsMessage());
            return CommandResult.KeepOpen();
        }

        return CommandResult.Dismiss();
    }
}
