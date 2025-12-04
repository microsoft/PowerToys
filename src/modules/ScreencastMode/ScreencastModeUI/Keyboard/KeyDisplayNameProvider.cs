// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using ManagedCommon;
using Windows.System;

namespace ScreencastModeUI.Keyboard
{
    /// <summary>
    /// Provides user-friendly display names for virtual keys optimized for screencast overlays.
    /// Uses a hybrid approach: hardcoded names for common keys (for consistency across keyboard layouts)
    /// and LayoutMap for special keys (media, browser, etc.).
    /// </summary>
    internal static class KeyDisplayNameProvider
    {
        private static readonly Lazy<PowerToys.Interop.LayoutMapManaged> _layoutMap =
            new Lazy<PowerToys.Interop.LayoutMapManaged>(() => new PowerToys.Interop.LayoutMapManaged());

        /// <summary>
        /// Gets a user-friendly display name for the specified virtual key.
        /// </summary>
        /// <param name="key">The virtual key to get the display name for.</param>
        /// <returns>A short, readable display name optimized for on-screen display.</returns>
        public static string GetKeyDisplayName(VirtualKey key)
        {
            // For screencast mode, we use custom short names optimized for on-screen display
            // These override the LayoutMap names for better readability during presentations
            return key switch
            {
                // Modifier keys - keep short for screen display
                VirtualKey.LeftWindows or VirtualKey.RightWindows => "Win",
                VirtualKey.Control => "Ctrl",
                VirtualKey.Menu => "Alt",
                VirtualKey.Shift => "Shift",

                // Special keys with symbols for compact display
                VirtualKey.Up => "↑",
                VirtualKey.Down => "↓",
                VirtualKey.Left => "←",
                VirtualKey.Right => "→",

                // Common keys with shortened names
                VirtualKey.Space => "Space",
                VirtualKey.Enter => "Enter",
                VirtualKey.Tab => "Tab",
                VirtualKey.Back => "Backspace",
                VirtualKey.Escape => "Esc",
                VirtualKey.Delete => "Del",
                VirtualKey.PageUp => "PgUp",
                VirtualKey.PageDown => "PgDn",
                VirtualKey.Home => "Home",
                VirtualKey.End => "End",
                VirtualKey.Insert => "Ins",

                // Numpad
                VirtualKey.NumberPad0 => "Num 0",
                VirtualKey.NumberPad1 => "Num 1",
                VirtualKey.NumberPad2 => "Num 2",
                VirtualKey.NumberPad3 => "Num 3",
                VirtualKey.NumberPad4 => "Num 4",
                VirtualKey.NumberPad5 => "Num 5",
                VirtualKey.NumberPad6 => "Num 6",
                VirtualKey.NumberPad7 => "Num 7",
                VirtualKey.NumberPad8 => "Num 8",
                VirtualKey.NumberPad9 => "Num 9",

                // F-keys
                VirtualKey.F1 => "F1",
                VirtualKey.F2 => "F2",
                VirtualKey.F3 => "F3",
                VirtualKey.F4 => "F4",
                VirtualKey.F5 => "F5",
                VirtualKey.F6 => "F6",
                VirtualKey.F7 => "F7",
                VirtualKey.F8 => "F8",
                VirtualKey.F9 => "F9",
                VirtualKey.F10 => "F10",
                VirtualKey.F11 => "F11",
                VirtualKey.F12 => "F12",

                // Letters A-Z - these will be uppercase regardless of keyboard layout
                >= VirtualKey.A and <= VirtualKey.Z => ((char)('A' + ((int)key - (int)VirtualKey.A))).ToString(),

                // Numbers 0-9 - semantic meaning, not keyboard layout dependent
                >= VirtualKey.Number0 and <= VirtualKey.Number9 => ((char)('0' + ((int)key - (int)VirtualKey.Number0))).ToString(),

                // OEM keys - use hardcoded US layout for consistency in screencasts
                // This ensures viewers see the semantic key regardless of presenter's keyboard
                (VirtualKey)0xBD => "-",        // VK_OEM_MINUS
                (VirtualKey)0xBB => "=",        // VK_OEM_PLUS
                (VirtualKey)0xDB => "[",        // VK_OEM_4
                (VirtualKey)0xDD => "]",        // VK_OEM_6
                (VirtualKey)0xDC => "\\",       // VK_OEM_5
                (VirtualKey)0xBA => ";",        // VK_OEM_1
                (VirtualKey)0xDE => "'",        // VK_OEM_7
                (VirtualKey)0xBC => ",",        // VK_OEM_COMMA
                (VirtualKey)0xBE => ".",        // VK_OEM_PERIOD
                (VirtualKey)0xBF => "/",        // VK_OEM_2
                (VirtualKey)0xC0 => "`",        // VK_OEM_3

                // For any other key, use the LayoutMap as fallback
                // This handles media keys, browser keys, and other special keys
                _ => GetLayoutMapKeyName(key),
            };
        }

        /// <summary>
        /// Checks if the specified key is a modifier key.
        /// </summary>
        /// <param name="key">The virtual key to check.</param>
        /// <returns>True if the key is a modifier (Shift, Ctrl, Alt, Win); otherwise, false.</returns>
        public static bool IsModifierKey(VirtualKey key)
        {
            return key is VirtualKey.Shift or
                   VirtualKey.LeftShift or
                   VirtualKey.RightShift or
                   VirtualKey.Control or
                   VirtualKey.LeftControl or
                   VirtualKey.RightControl or
                   VirtualKey.Menu or // Alt
                   VirtualKey.LeftMenu or
                   VirtualKey.RightMenu or
                   VirtualKey.LeftWindows or
                   VirtualKey.RightWindows;
        }

        /// <summary>
        /// Normalizes modifier keys to their generic form (e.g., LeftShift -> Shift).
        /// </summary>
        /// <param name="key">The modifier key to normalize.</param>
        /// <returns>The normalized modifier key.</returns>
        public static VirtualKey NormalizeModifierKey(VirtualKey key)
        {
            return key switch
            {
                VirtualKey.LeftShift or VirtualKey.RightShift => VirtualKey.Shift,
                VirtualKey.LeftControl or VirtualKey.RightControl => VirtualKey.Control,
                VirtualKey.LeftMenu or VirtualKey.RightMenu => VirtualKey.Menu,
                VirtualKey.LeftWindows or VirtualKey.RightWindows => VirtualKey.LeftWindows,
                _ => key,
            };
        }

        /// <summary>
        /// Checks if the specified key should trigger clearing the keystroke display.
        /// </summary>
        /// <param name="key">The virtual key to check.</param>
        /// <returns>True if the key should clear the display; otherwise, false.</returns>
        public static bool IsClearKey(VirtualKey key)
        {
            return key is VirtualKey.Up or
                   VirtualKey.Down or
                   VirtualKey.Left or
                   VirtualKey.Right or

                   // VirtualKey.Back or
                   VirtualKey.Escape;
        }

        /// <summary>
        /// Gets the display order priority for modifier keys.
        /// Lower values appear first in the shortcut display.
        /// </summary>
        /// <param name="key">The modifier key.</param>
        /// <returns>An integer representing the display order (0 = highest priority).</returns>
        public static int GetModifierOrder(VirtualKey key)
        {
            return key switch
            {
                VirtualKey.LeftWindows => 0,
                VirtualKey.Control => 1,
                VirtualKey.Menu => 2,
                VirtualKey.Shift => 3,
                _ => 4,
            };
        }

        /// <summary>
        /// Gets the key name from LayoutMap with shortened verbose names for better screen display.
        /// </summary>
        /// <param name="key">The virtual key to look up.</param>
        /// <returns>A display name from LayoutMap, shortened for readability.</returns>
        private static string GetLayoutMapKeyName(VirtualKey key)
        {
            try
            {
                var keyName = _layoutMap.Value.GetKeyName((uint)key);

                // Shorten some verbose names from LayoutMap for better screen display
                return keyName switch
                {
                    "Win (Left)" or "Win (Right)" => "Win",
                    "Ctrl (Left)" or "Ctrl (Right)" => "Ctrl",
                    "Alt (Left)" or "Alt (Right)" => "Alt",
                    "Shift (Left)" or "Shift (Right)" => "Shift",
                    "Print Screen" => "PrtScn",
                    "Caps Lock" => "CapsLk",
                    "Num Lock" => "NumLk",
                    "Scroll Lock" => "ScrLk",
                    "Apps/Menu" => "Menu",
                    _ => keyName,
                };
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Failed to get key name from LayoutMap for key {key}: {ex.Message}");

                // Fallback to virtual key name
                return key.ToString();
            }
        }
    }
}
