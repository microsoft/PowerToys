// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ManagedCommon
{
    [ComVisible(true)]
    public static class KeyNameLocalisation
    {
        /// <summary>
        /// Gets the localisation of a keyboard key
        /// </summary>
        /// <param name="keyName">The name of the key</param>
        /// <returns>The localised key. If none gets found it just returns <paramref name="keyName"/>.</returns>
        [ComVisible(true)]
        public static string GetLocalisation(string keyName)
        {
            return keyName switch
            {
                "Alt" => Properties.Resources.Keyboard_Alt,
                "Apps/Menu" => Properties.Resources.Keyboard_AppsMenu,
                "Caps Lock" => Properties.Resources.Keyboard_CapsLock,
                "Clear" => Properties.Resources.Keyboard_Clear,
                "Ctrl" => Properties.Resources.Keyboard_Ctrl,
                "Delete" => Properties.Resources.Keyboard_Delete,
                "Down" => Properties.Resources.Keyboard_Down,
                "End" => Properties.Resources.Keyboard_End,
                "Esc" => Properties.Resources.Keyboard_Esc,
                "Execute" => Properties.Resources.Keyboard_Execute,
                "Help" => Properties.Resources.Keyboard_Help,
                "Home" => Properties.Resources.Keyboard_Home,
                "Insert" => Properties.Resources.Keyboard_Insert,
                "Left" => Properties.Resources.Keybaord_Left,
                "Num Lock" => Properties.Resources.Keyboard_NumLock,
                "Pause" => Properties.Resources.Keyboard_Pause,
                "PgDn" => Properties.Resources.Keyboard_PageDown,
                "PgUp" => Properties.Resources.Keyboard_PageUp,
                "Print" => Properties.Resources.Keyboard_Print,
                "Print Screen" => Properties.Resources.Keyboard_PrintScreen,
                "Right" => Properties.Resources.Keyboard_Right,
                "Scroll Lock" => Properties.Resources.Keyboard_ScrollLock,
                "Select" => Properties.Resources.Keyboard_Select,
                "Shift" => Properties.Resources.Keyboard_Shift,
                "Tab" => Properties.Resources.Keyboard_Tab,
                "Up" => Properties.Resources.Keyboard_Up,
                _ => keyName,
            };
        }
    }
}
