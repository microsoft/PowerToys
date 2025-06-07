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
        LCtrl,
        RCtrl,
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

        public static void PressKey(Key key)
        {
            PressVirtualKey(TranslateKeyHex(key));
        }

        public static void ReleaseKey(Key key)
        {
            ReleaseVirtualKey(TranslateKeyHex(key));
        }

        public static void SendKey(Key key)
        {
            PressVirtualKey(TranslateKeyHex(key));
            ReleaseVirtualKey(TranslateKeyHex(key));
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
                case Key.LCtrl:
                    return "^";
                case Key.RCtrl:
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
                    return "a";
                case Key.B:
                    return "b";
                case Key.C:
                    return "c";
                case Key.D:
                    return "d";
                case Key.E:
                    return "e";
                case Key.F:
                    return "f";
                case Key.G:
                    return "g";
                case Key.H:
                    return "h";
                case Key.I:
                    return "i";
                case Key.J:
                    return "j";
                case Key.K:
                    return "k";
                case Key.L:
                    return "l";
                case Key.M:
                    return "m";
                case Key.N:
                    return "n";
                case Key.O:
                    return "o";
                case Key.P:
                    return "p";
                case Key.Q:
                    return "q";
                case Key.R:
                    return "r";
                case Key.S:
                    return "s";
                case Key.T:
                    return "t";
                case Key.U:
                    return "u";
                case Key.V:
                    return "v";
                case Key.W:
                    return "w";
                case Key.X:
                    return "x";
                case Key.Y:
                    return "y";
                case Key.Z:
                    return "z";
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
        /// map the virtual key codes to the corresponding keys.
        /// </summary>
        private static byte TranslateKeyHex(Key key)
        {
            switch (key)
            {
                case Key.Win:
                    return 0x5B;  // Windows Key - 0x5B in hex
                case Key.Ctrl:
                    return 0x11;  // Ctrl Key - 0x11 in hex
                case Key.Alt:
                    return 0x12;  // Alt Key - 0x12 in hex
                case Key.Shift:
                    return 0x10;  // Shift Key - 0x10 in hex
                case Key.LCtrl:
                    return 0xA2;  // Left Ctrl Key - 0xA2 in hex
                case Key.RCtrl:
                    return 0xA3;  // Right Ctrl Key - 0xA3 in hex
                case Key.A:
                    return 0x41;  // A Key - 0x41 in hex
                case Key.B:
                    return 0x42;  // B Key - 0x42 in hex
                case Key.C:
                    return 0x43;  // C Key - 0x43 in hex
                case Key.D:
                    return 0x44;  // D Key - 0x44 in hex
                case Key.E:
                    return 0x45;  // E Key - 0x45 in hex
                case Key.F:
                    return 0x46;  // F Key - 0x46 in hex
                case Key.G:
                    return 0x47;  // G Key - 0x47 in hex
                case Key.H:
                    return 0x48;  // H Key - 0x48 in hex
                case Key.I:
                    return 0x49;  // I Key - 0x49 in hex
                case Key.J:
                    return 0x4A;  // J Key - 0x4A in hex
                case Key.K:
                    return 0x4B;  // K Key - 0x4B in hex
                case Key.L:
                    return 0x4C;  // L Key - 0x4C in hex
                case Key.M:
                    return 0x4D;  // M Key - 0x4D in hex
                case Key.N:
                    return 0x4E;  // N Key - 0x4E in hex
                case Key.O:
                    return 0x4F;  // O Key - 0x4F in hex
                case Key.P:
                    return 0x50;  // P Key - 0x50 in hex
                case Key.Q:
                    return 0x51;  // Q Key - 0x51 in hex
                case Key.R:
                    return 0x52;  // R Key - 0x52 in hex
                case Key.S:
                    return 0x53;  // S Key - 0x53 in hex
                case Key.T:
                    return 0x54;  // T Key - 0x54 in hex
                case Key.U:
                    return 0x55;  // U Key - 0x55 in hex
                case Key.V:
                    return 0x56;  // V Key - 0x56 in hex
                case Key.W:
                    return 0x57;  // W Key - 0x57 in hex
                case Key.X:
                    return 0x58;  // X Key - 0x58 in hex
                case Key.Y:
                    return 0x59;  // Y Key - 0x59 in hex
                case Key.Z:
                    return 0x5A;  // Z Key - 0x5A in hex
                case Key.Num0:
                    return 0x30;  // 0 Key - 0x30 in hex
                case Key.Num1:
                    return 0x31;  // 1 Key - 0x31 in hex
                case Key.Num2:
                    return 0x32;  // 2 Key - 0x32 in hex
                case Key.Num3:
                    return 0x33;  // 3 Key - 0x33 in hex
                case Key.Num4:
                    return 0x34;  // 4 Key - 0x34 in hex
                case Key.Num5:
                    return 0x35;  // 5 Key - 0x35 in hex
                case Key.Num6:
                    return 0x36;  // 6 Key - 0x36 in hex
                case Key.Num7:
                    return 0x37;  // 7 Key - 0x37 in hex
                case Key.Num8:
                    return 0x38;  // 8 Key - 0x38 in hex
                case Key.Num9:
                    return 0x39;  // 9 Key - 0x39 in hex
                case Key.F1:
                    return 0x70;  // F1 Key - 0x70 in hex
                case Key.F2:
                    return 0x71;  // F2 Key - 0x71 in hex
                case Key.F3:
                    return 0x72;  // F3 Key - 0x72 in hex
                case Key.F4:
                    return 0x73;  // F4 Key - 0x73 in hex
                case Key.F5:
                    return 0x74;  // F5 Key - 0x74 in hex
                case Key.F6:
                    return 0x75;  // F6 Key - 0x75 in hex
                case Key.F7:
                    return 0x76;  // F7 Key - 0x76 in hex
                case Key.F8:
                    return 0x77;  // F8 Key - 0x77 in hex
                case Key.F9:
                    return 0x78;  // F9 Key - 0x78 in hex
                case Key.F10:
                    return 0x79;  // F10 Key - 0x79 in hex
                case Key.F11:
                    return 0x7A;  // F11 Key - 0x7A in hex
                case Key.F12:
                    return 0x7B;  // F12 Key - 0x7B in hex
                case Key.Up:
                    return 0x26;  // Up Arrow Key - 0x26 in hex
                case Key.Down:
                    return 0x28;  // Down Arrow Key - 0x28 in hex
                case Key.Left:
                    return 0x25;  // Left Arrow Key - 0x25 in hex
                case Key.Right:
                    return 0x27;  // Right Arrow Key - 0x27 in hex
                case Key.Home:
                    return 0x24;  // Home Key - 0x24 in hex
                case Key.End:
                    return 0x23;  // End Key - 0x23 in hex
                case Key.PageUp:
                    return 0x21;  // Page Up Key - 0x21 in hex
                case Key.PageDown:
                    return 0x22;  // Page Down Key - 0x22 in hex
                case Key.Space:
                    return 0x20;  // Space Key - 0x20 in hex
                case Key.Enter:
                    return 0x0D;  // Enter Key - 0x0D in hex
                case Key.Backspace:
                    return 0x08;  // Backspace Key - 0x08 in hex
                case Key.Tab:
                    return 0x09;  // Tab Key - 0x09 in hex
                case Key.Esc:
                    return 0x1B;  // Escape Key - 0x1B in hex
                case Key.Insert:
                    return 0x2D;  // Insert Key - 0x2D in hex
                case Key.Delete:
                    return 0x2E;  // Delete Key - 0x2E in hex
                default:
                    throw new ArgumentException($"Key {key} is not supported, Please add your key at TranslateKeyHex for translation to hex.");
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

        /// <summary>
        /// Just press the key.(no release)
        /// </summary>
        private static void PressVirtualKey(byte key)
        {
            keybd_event(key, 0, KEYEVENTF_KEYDOWN, UIntPtr.Zero);
        }

        /// <summary>
        /// Release only the button (if pressed first)
        /// </summary>
        private static void ReleaseVirtualKey(byte key)
        {
            keybd_event(key, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }
    }
}
