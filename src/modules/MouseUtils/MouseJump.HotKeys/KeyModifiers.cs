// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace MouseJump.HotKeys;

[Flags]
public enum KeyModifiers
{
    None = 0,
    Alt = (int)HOT_KEY_MODIFIERS.MOD_ALT,
    Control = (int)HOT_KEY_MODIFIERS.MOD_CONTROL,
    Shift = (int)HOT_KEY_MODIFIERS.MOD_SHIFT,
    Windows = (int)HOT_KEY_MODIFIERS.MOD_WIN,
    NoRepeat = (int)HOT_KEY_MODIFIERS.MOD_NOREPEAT,
}
