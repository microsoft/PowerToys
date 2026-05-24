// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Input;
using Windows.System;
using Windows.UI.Core;

namespace Microsoft.CmdPal.UI.Helpers;

/// <summary>
/// Snapshot of the current keyboard modifier state (Ctrl, Alt, Shift, Win).
/// </summary>
internal readonly struct KeyModifiers
{
    public bool Ctrl { get; }

    public bool Alt { get; }

    public bool Shift { get; }

    public bool Win { get; }

    private KeyModifiers(bool ctrl, bool alt, bool shift, bool win)
    {
        Ctrl = ctrl;
        Alt = alt;
        Shift = shift;
        Win = win;
    }

    /// <summary>
    /// Gets a snapshot of the modifier keys currently held down.
    /// </summary>
    public static KeyModifiers GetCurrent()
    {
        var ctrl = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control).HasFlag(CoreVirtualKeyStates.Down);
        var alt = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Menu).HasFlag(CoreVirtualKeyStates.Down);
        var shift = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift).HasFlag(CoreVirtualKeyStates.Down);
        var win = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.LeftWindows).HasFlag(CoreVirtualKeyStates.Down) ||
                  InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.RightWindows).HasFlag(CoreVirtualKeyStates.Down);
        return new KeyModifiers(ctrl, alt, shift, win);
    }

    public bool OnlyAlt => Alt && !Ctrl && !Shift && !Win;

    public bool OnlyCtrl => Ctrl && !Alt && !Shift && !Win;

    public bool None => !Ctrl && !Alt && !Shift && !Win;
}
