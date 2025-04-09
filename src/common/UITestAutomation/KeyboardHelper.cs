// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PowerToys.UITest
{
    /// <summary>
    /// Provides methods for simulating keyboard input.
    /// </summary>
    internal static class KeyboardHelper
    {
        [DllImport("user32.dll")]
#pragma warning disable SA1300 // Element should begin with upper-case letter
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
#pragma warning restore SA1300 // Element should begin with upper-case letter

#pragma warning disable SA1310 // Field names should not contain underscore
        private const byte VK_LWIN = 0x5B;
        private const uint KEYEVENTF_KEYDOWN = 0x0000;
        private const uint KEYEVENTF_KEYUP = 0x0002;
#pragma warning restore SA1310 // Field names should not contain underscore

        /// <summary>
        /// Simulate keyboard input
        /// </summary>
        /// <param name="key1">The first key to send.</param>
        /// <param name="key2">The second key to send (optional).</param>
        /// <param name="key3">The third key to send (optional).</param>
        /// <param name="key4">The fourth key to send (optional).</param>
        public static void SendKeys(string key1, string key2 = "", string key3 = "", string key4 = "")
        {
            string keysToSend = TranslateKey(key1) + TranslateKey(key2) + TranslateKey(key3) + TranslateKey(key4);
            SendWinKeyCombination(keysToSend);
        }

        /// <summary>
        /// Translates a key to its corresponding SendKeys representation.
        /// </summary>
        /// <param name="key">The key to translate.</param>
        /// <returns>The SendKeys representation of the key.</returns>
        private static string TranslateKey(string key)
        {
            switch (key.ToLower())
            {
                case "ctrl":
                    return "^";
                case "alt":
                    return "%";
                case "shift":
                    return "+";
                case "tab":
                    return "{TAB}";
                case "esc":
                    return "{ESC}";
                case "enter":
                    return "{ENTER}";
                case "win":
                    return "{WIN}";

                default:
                    return key;
            }
        }

        /// <summary>
        /// Sends a combination of keys, including the Windows key, to the system.
        /// </summary>
        /// <param name="keys">The keys to send.</param>
        private static void SendWinKeyCombination(string keys)
        {
            bool winKeyDown = false;

            if (keys.Contains("{WIN}"))
            {
                keybd_event(VK_LWIN, 0, KEYEVENTF_KEYDOWN, UIntPtr.Zero);
                winKeyDown = true;
                keys = keys.Replace("{WIN}", string.Empty); // Remove {WIN} from the string
            }

            System.Windows.Forms.SendKeys.SendWait(keys);

            // Release Windows key
            if (winKeyDown)
            {
                keybd_event(VK_LWIN, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            }
        }
    }
}
