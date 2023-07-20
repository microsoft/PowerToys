// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using MouseJumpUI.HotKeys;

namespace MouseJumpUI.HotKeys;

public sealed class HotKeyEventArgs : EventArgs
{
    public HotKeyEventArgs(Keys key, KeyModifiers modifiers)
    {
        this.Key = key;
        this.Modifiers = modifiers;
    }

    public Keys Key
    {
        get;
    }

    public KeyModifiers Modifiers
    {
        get;
    }
}
