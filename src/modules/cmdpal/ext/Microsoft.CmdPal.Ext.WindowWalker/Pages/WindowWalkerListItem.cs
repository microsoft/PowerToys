// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.WindowWalker.Commands;
using Microsoft.CmdPal.Ext.WindowWalker.Components;

namespace Microsoft.CmdPal.Ext.WindowWalker.Pages;

internal sealed partial class WindowWalkerListItem : ListItem
{
    public Window? Window { get; }

    public WindowWalkerListItem(Window? window)
        : base(new SwitchToWindowCommand(window))
    {
        Window = window;
    }
}
