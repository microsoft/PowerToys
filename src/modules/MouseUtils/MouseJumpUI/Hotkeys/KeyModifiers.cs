// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using MouseJumpUI.NativeMethods;

namespace MouseJumpUI.HotKeys;

[Flags]
public enum KeyModifiers
{
    None = 0,
    Alt = (int)User32.HOT_KEY_MODIFIERS.MOD_ALT,
    Control = (int)User32.HOT_KEY_MODIFIERS.MOD_CONTROL,
    Shift = (int)User32.HOT_KEY_MODIFIERS.MOD_SHIFT,
    Windows = (int)User32.HOT_KEY_MODIFIERS.MOD_WIN,
    NoRepeat = (int)User32.HOT_KEY_MODIFIERS.MOD_NOREPEAT,
}
