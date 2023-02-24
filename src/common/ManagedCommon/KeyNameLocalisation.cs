// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ManagedCommon
{
    public static class KeyNameLocalisation
    {
        /// <summary>
        /// Gets the localisation of a keyboard key
        /// </summary>
        /// <param name="keyName">The name of the key</param>
        /// <returns>The localised key. False if none was found.</returns>
        public static string GetLocalisation(string keyName)
        {
            return keyName switch
            {
                "Alt" => Properties.Resources.Keyboard_Alt,
                "Apps/Menu" => Properties.Resources.Keyboard_AppsMenu,
                "Caps Lock" => Properties.Resources.Keyboard_CapsLock,
                "Ctrl" => Properties.Resources.Keyboard_Ctrl,
                "Delete" => Properties.Resources.Keyboard_Delete,
                "End" => Properties.Resources.Keyboard_End,
                "Esc" => Properties.Resources.Keyboard_Esc,
                "Home" => Properties.Resources.Keyboard_Home,
                "Insert" => Properties.Resources.Keyboard_Insert,
                "Num Lock" => Properties.Resources.Keyboard_NumLock,
                "PgDn" => Properties.Resources.Keyboard_PageDown,
                "PgUp" => Properties.Resources.Keyboard_PageUp,
                "Pause" => Properties.Resources.Keyboard_Pause,
                "Print Screen" => Properties.Resources.Keyboard_PrintScreen,
                "Scroll Lock" => Properties.Resources.Keyboard_ScrollLock,
                "Shift" => Properties.Resources.Keyboard_Shift,
                "Tab" => Properties.Resources.Keyboard_Tab,
                _ => null,
            };
        }
    }
}
