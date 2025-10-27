// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.WindowWalker.Commands;
using Microsoft.CmdPal.Ext.WindowWalker.Components;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.WindowWalker;

internal sealed partial class WindowWalkerListItem : ListItem
{
    private readonly Window? _window;

    public Window? Window => _window;

    public WindowWalkerListItem(Window? window)
        : base(new SwitchToWindowCommand(window))
    {
        _window = window;
    }

    public override bool Equals(object? obj)
    {
        if (obj is WindowWalkerListItem other)
        {
            if (this._window is null)
            {
                return other._window is null;
            }

            return this._window.Equals(other._window);
        }

        return base.Equals(obj);
    }

    public override int GetHashCode()
    {
        return base.GetHashCode();
    }
}
