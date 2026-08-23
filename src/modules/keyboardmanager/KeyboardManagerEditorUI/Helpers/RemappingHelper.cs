// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Windows.System;

namespace KeyboardManagerEditorUI.Helpers
{
    public static class RemappingHelper
    {
        public static bool IsModifierKey(VirtualKey key)
        {
            return key == VirtualKey.Control
                || key == VirtualKey.LeftControl
                || key == VirtualKey.RightControl
                || key == VirtualKey.Menu
                || key == VirtualKey.LeftMenu
                || key == VirtualKey.RightMenu
                || key == VirtualKey.Shift
                || key == VirtualKey.LeftShift
                || key == VirtualKey.RightShift
                || key == VirtualKey.LeftWindows
                || key == VirtualKey.RightWindows;
        }

        /// <summary>
        /// Returns the sort order for a modifier key in the standard display order:
        /// Win(0) → Ctrl(1) → Alt(2) → Shift(3).
        /// Non-modifier (action) keys return 4.
        /// This matches the old C++ GetKeyVector behavior.
        /// </summary>
        public static int GetModifierSortOrder(VirtualKey key)
        {
            return key switch
            {
                VirtualKey.LeftWindows or VirtualKey.RightWindows => 0,
                VirtualKey.Control or VirtualKey.LeftControl or VirtualKey.RightControl => 1,
                VirtualKey.Menu or VirtualKey.LeftMenu or VirtualKey.RightMenu => 2,
                VirtualKey.Shift or VirtualKey.LeftShift or VirtualKey.RightShift => 3,
                _ => 4,
            };
        }

        /// <summary>
        /// Sorts a list of modifier keys in the standard display order:
        /// Win → Ctrl → Alt → Shift.
        /// </summary>
        public static void SortModifierKeys(List<VirtualKey> modifierKeys)
        {
            modifierKeys.Sort((a, b) => GetModifierSortOrder(a).CompareTo(GetModifierSortOrder(b)));
        }
    }
}
