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
    /// Represents keyboard keys.
    /// </summary>
    public enum Key
    {
        Ctrl,
        Alt,
        Shift,
        Tab,
        Esc,
        Enter,
        Win,
        A,
        B,
        C,
        D,
        E,
        F,
        G,
        H,
        I,
        J,
        K,
        L,
        M,
        N,
        O,
        P,
        Q,
        R,
        S,
        T,
        U,
        V,
        W,
        X,
        Y,
        Z,
        Num0,
        Num1,
        Num2,
        Num3,
        Num4,
        Num5,
        Num6,
        Num7,
        Num8,
        Num9,
        F1,
        F2,
        F3,
        F4,
        F5,
        F6,
        F7,
        F8,
        F9,
        F10,
        F11,
        F12,
        Space,
        Backspace,
        Delete,
        Insert,
        Home,
        End,
        PageUp,
        PageDown,
        Up,
        Down,
        Left,
        Right,
        Other,
    }

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
        /// Sends a combination of keys.
        /// </summary>
        /// <param name="keys">The keys to send.</param>
        public static void SendKeys(params Key[] keys)
        {
            string keysToSend = string.Join(string.Empty, keys.Select(TranslateKey));
            SendWinKeyCombination(keysToSend);
        }

        /// <summary>
        /// Translates a key to its corresponding SendKeys representation.
        /// </summary>
        /// <param name="key">The key to translate.</param>
        /// <returns>The SendKeys representation of the key.</returns>
        private static string TranslateKey(Key key)
        {
            switch (key)
            {
                case Key.Ctrl:
                    return "^";
                case Key.Alt:
                    return "%";
                case Key.Shift:
                    return "+";
                case Key.Tab:
                    return "{TAB}";
                case Key.Esc:
                    return "{ESC}";
                case Key.Enter:
                    return "{ENTER}";
                case Key.Win:
                    return "{WIN}";
                case Key.Space:
                    return " ";
                case Key.Backspace:
                    return "{BACKSPACE}";
                case Key.Delete:
                    return "{DELETE}";
                case Key.Insert:
                    return "{INSERT}";
                case Key.Home:
                    return "{HOME}";
                case Key.End:
                    return "{END}";
                case Key.PageUp:
                    return "{PGUP}";
                case Key.PageDown:
                    return "{PGDN}";
                case Key.Up:
                    return "{UP}";
                case Key.Down:
                    return "{DOWN}";
                case Key.Left:
                    return "{LEFT}";
                case Key.Right:
                    return "{RIGHT}";
                case Key.F1:
                    return "{F1}";
                case Key.F2:
                    return "{F2}";
                case Key.F3:
                    return "{F3}";
                case Key.F4:
                    return "{F4}";
                case Key.F5:
                    return "{F5}";
                case Key.F6:
                    return "{F6}";
                case Key.F7:
                    return "{F7}";
                case Key.F8:
                    return "{F8}";
                case Key.F9:
                    return "{F9}";
                case Key.F10:
                    return "{F10}";
                case Key.F11:
                    return "{F11}";
                case Key.F12:
                    return "{F12}";
                case Key.A:
                    return "A";
                case Key.B:
                    return "B";
                case Key.C:
                    return "C";
                case Key.D:
                    return "D";
                case Key.E:
                    return "E";
                case Key.F:
                    return "F";
                case Key.G:
                    return "G";
                case Key.H:
                    return "H";
                case Key.I:
                    return "I";
                case Key.J:
                    return "J";
                case Key.K:
                    return "K";
                case Key.L:
                    return "L";
                case Key.M:
                    return "M";
                case Key.N:
                    return "N";
                case Key.O:
                    return "O";
                case Key.P:
                    return "P";
                case Key.Q:
                    return "Q";
                case Key.R:
                    return "R";
                case Key.S:
                    return "S";
                case Key.T:
                    return "T";
                case Key.U:
                    return "U";
                case Key.V:
                    return "V";
                case Key.W:
                    return "W";
                case Key.X:
                    return "X";
                case Key.Y:
                    return "Y";
                case Key.Z:
                    return "Z";
                case Key.Num0:
                    return "0";
                case Key.Num1:
                    return "1";
                case Key.Num2:
                    return "2";
                case Key.Num3:
                    return "3";
                case Key.Num4:
                    return "4";
                case Key.Num5:
                    return "5";
                case Key.Num6:
                    return "6";
                case Key.Num7:
                    return "7";
                case Key.Num8:
                    return "8";
                case Key.Num9:
                    return "9";
                default:
                    return string.Empty;
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
