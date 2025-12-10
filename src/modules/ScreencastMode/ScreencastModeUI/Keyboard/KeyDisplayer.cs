// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text;
using ManagedCommon;
using Windows.System;

namespace ScreencastModeUI.Keyboard
{
    /// <summary>
    /// Manages keystroke display for screencast overlays.
    /// Tracks key state and builds display strings optimized for on-screen presentation.
    /// </summary>
    internal sealed class KeyDisplayer
    {
        private static readonly Lazy<PowerToys.Interop.LayoutMapManaged> _layoutMap =
            new Lazy<PowerToys.Interop.LayoutMapManaged>(() => new PowerToys.Interop.LayoutMapManaged());

        // Track displayed keys in order where each entry is a display string
        private readonly List<string> _displayedKeys = new();

        // Track currently held modifiers
        private readonly HashSet<VirtualKey> _activeModifiers = new();

        // Flag to track if we need to add "+" before the next key
        private bool _needsPlusSeparator;

        /// <summary>
        /// Event raised when the display text has been updated and the UI should refresh.
        /// </summary>
        public event EventHandler? DisplayUpdated;

        /// <summary>
        /// Gets the current display text for the keystroke overlay.
        /// </summary>
        public string DisplayText => BuildDisplayText();

        /// <summary>
        /// Gets a value indicating whether there is content to display.
        /// </summary>
        public bool HasContent => _displayedKeys.Count > 0;

        /// <summary>
        /// Processes a key event (key down or key up).
        /// </summary>
        /// <param name="key">The virtual key.</param>
        /// <param name="isKeyDown">True if key is pressed; false if released.</param>
        public void ProcessKeyEvent(VirtualKey key, bool isKeyDown)
        {
            if (isKeyDown)
            {
                HandleKeyDown(key);
            }
            else
            {
                HandleKeyUp(key);
            }
        }

        /// <summary>
        /// Clears all tracked keys and modifiers.
        /// </summary>
        public void Clear()
        {
            _displayedKeys.Clear();
            _activeModifiers.Clear();
            _needsPlusSeparator = false;
        }

        /// <summary>
        /// Handle when a key is being pressed.
        /// </summary>
        /// <param name="key">The key that is currently being held down.</param>
        private void HandleKeyDown(VirtualKey key)
        {
            // Normalize modifier keys (e.g., LeftShift -> Shift)
            var normalizedKey = IsModifierKey(key)
                ? NormalizeModifierKey(key)
                : key;

            // Handle modifier keys
            if (IsModifierKey(key))
            {
                // Only add modifier if not already held (Add returns false if already present)
                if (_activeModifiers.Add(normalizedKey))
                {
                    var keyName = GetKeyDisplayName(normalizedKey);

                    // Check if adding would overflow
                    string previewText = BuildPreviewText(keyName);
                    if (WillOverflow(previewText))
                    {
                        // Clear and start fresh with just this modifier
                        _displayedKeys.Clear();
                        _needsPlusSeparator = false;
                    }

                    // Add "+" if we already have content and need separator
                    if (_needsPlusSeparator && _displayedKeys.Count > 0)
                    {
                        _displayedKeys.Add("+");
                    }

                    _displayedKeys.Add(keyName);

                    // Next key should have a "+" before it
                    _needsPlusSeparator = true;
                }
            }

            // Backspace and Escape keys clear the current display
            else if (IsClearKey(key))
            {
                // Clear keys (Backspace, Esc) - clear and show just this key
                _displayedKeys.Clear();
                _activeModifiers.Clear();
                _needsPlusSeparator = false;

                _displayedKeys.Add(GetKeyDisplayName(normalizedKey));
                _needsPlusSeparator = false; // Clear keys don't expect continuation
            }
            else
            {
                // Regular key
                var keyName = GetKeyDisplayName(normalizedKey);

                // Check if adding would overflow
                string previewText = BuildPreviewText(keyName);
                if (WillOverflow(previewText))
                {
                    // Clear and start fresh - but keep active modifiers shown
                    _displayedKeys.Clear();
                    _needsPlusSeparator = false;

                    // Re-add currently held modifiers
                    foreach (var mod in _activeModifiers)
                    {
                        if (_displayedKeys.Count > 0)
                        {
                            _displayedKeys.Add("+");
                        }

                        _displayedKeys.Add(GetKeyDisplayName(mod));
                    }

                    if (_displayedKeys.Count > 0)
                    {
                        _needsPlusSeparator = true;
                    }
                }

                // Add "+" if we have modifiers held or previous content
                if (_needsPlusSeparator && _displayedKeys.Count > 0)
                {
                    _displayedKeys.Add("+");
                }

                _displayedKeys.Add(keyName);

                // If modifiers are still held, next key should have "+"
                // If no modifiers, this is a standalone key, so start fresh next time
                _needsPlusSeparator = _activeModifiers.Count > 0;
            }

            DisplayUpdated?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Handle key release events.
        /// </summary>
        /// <param name="key">The key that is released.</param>
        private void HandleKeyUp(VirtualKey key)
        {
            if (IsModifierKey(key))
            {
                var normalizedKey = NormalizeModifierKey(key);
                _activeModifiers.Remove(normalizedKey);

                // When all modifiers are released, reset the separator flag
                // This allows the next keystroke to start a new sequence
                if (_activeModifiers.Count == 0)
                {
                    _needsPlusSeparator = false;
                }
            }
        }

        /// <summary>
        /// Builds the display text from the displayed keys list.
        /// Keys are shown in the exact order they were added.
        /// </summary>
        private string BuildDisplayText()
        {
            if (_displayedKeys.Count == 0)
            {
                return string.Empty;
            }

            // Join with spaces for visual separation, but "+" entries are already in the list
            var result = new StringBuilder();
            foreach (var part in _displayedKeys)
            {
                if (part == "+")
                {
                    // Add space before and after the plus for readability
                    result.Append(" + ");
                }
                else
                {
                    if (result.Length > 0 && !result.ToString().EndsWith(' '))
                    {
                        // Only add space if not coming right after a "+"
                        // Check if last thing added was " + "
                        if (!result.ToString().EndsWith("+ ", StringComparison.Ordinal))
                        {
                            result.Append(' ');
                        }
                    }

                    result.Append(part);
                }
            }

            return result.ToString().Trim();
        }

        /// <summary>
        /// Builds a preview of what the display text would look like if we add a new key.
        /// </summary>
        private string BuildPreviewText(string newKey)
        {
            var tempList = new List<string>(_displayedKeys);
            if (_needsPlusSeparator && tempList.Count > 0)
            {
                tempList.Add("+");
            }

            tempList.Add(newKey);

            var result = new StringBuilder();
            foreach (var part in tempList)
            {
                if (part == "+")
                {
                    result.Append(" + ");
                }
                else
                {
                    if (result.Length > 0 && !result.ToString().EndsWith(' '))
                    {
                        if (!result.ToString().EndsWith("+ ", StringComparison.Ordinal))
                        {
                            result.Append(' ');
                        }
                    }

                    result.Append(part);
                }
            }

            return result.ToString().Trim();
        }

        private static bool WillOverflow(string nextText)
        {
            // Rough width check using character count vs. a max visible chars estimate
            const int maxVisibleChars = 40; // tune this based on your font/width
            return nextText.Length > maxVisibleChars;
        }

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
            return key is VirtualKey.Back or
                   VirtualKey.Escape;
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
