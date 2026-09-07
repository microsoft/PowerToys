// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.WindowWalker.Commands;
using Microsoft.CmdPal.Ext.WindowWalker.Components;

namespace Microsoft.CmdPal.Ext.WindowWalker.Pages;

internal sealed partial class WindowWalkerListItem : ListItem
{
    private readonly SwitchToWindowCommand _switchToWindowCommand;

    public Window? Window { get; private set; }

    internal bool NeedsIconLoad => _switchToWindowCommand.NeedsIconLoad;

    public WindowWalkerListItem(Window? window)
        : this(window, new SwitchToWindowCommand(window))
    {
    }

    private WindowWalkerListItem(Window? window, SwitchToWindowCommand command)
        : base(command)
    {
        Window = window;
        _switchToWindowCommand = command;
    }

    internal void UpdateWindow(Window window)
    {
        Window = window;
        _switchToWindowCommand.UpdateWindow(window);
    }

    internal void LoadIcon() => _switchToWindowCommand.LoadIcon();
}
