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
    public class Program
    {
        private readonly UIntPtr ignoreKeyEventFlag = 0x5555;

        [DllImport("user32.dll")]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [StructLayout(LayoutKind.Sequential)]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1307:Accessible fields should begin with upper-case letter", Justification = "Matching Native Structure")]
        internal struct INPUT
        {
            internal INPUTTYPE type;
            internal InputUnion data;

            internal static int Size
            {
                get { return Marshal.SizeOf<INPUT>(); }
            }
        }

        [StructLayout(LayoutKind.Explicit)]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1307:Accessible fields should begin with upper-case letter", Justification = "Matching Native Structure")]
        internal struct InputUnion
        {
            [FieldOffset(0)]
            internal MOUSEINPUT mi;
            [FieldOffset(0)]
            internal KEYBDINPUT ki;
            [FieldOffset(0)]
            internal HARDWAREINPUT hi;
        }

        [StructLayout(LayoutKind.Sequential)]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1307:Accessible fields should begin with upper-case letter", Justification = "Matching Native Structure")]
        internal struct MOUSEINPUT
        {
            internal int dx;
            internal int dy;
            internal int mouseData;
            internal uint dwFlags;
            internal uint time;
            internal UIntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1307:Accessible fields should begin with upper-case letter", Justification = "Matching Native Structure")]
        internal struct KEYBDINPUT
        {
            internal short wVk;
            internal short wScan;
            internal uint dwFlags;
            internal int time;
            internal UIntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1307:Accessible fields should begin with upper-case letter", Justification = "Matching Native Structure")]
        internal struct HARDWAREINPUT
        {
            internal int uMsg;
            internal short wParamL;
            internal short wParamH;
        }

        internal enum INPUTTYPE : uint
        {
            INPUT_MOUSE = 0,
            INPUT_KEYBOARD = 1,
            INPUT_HARDWARE = 2,
        }

        [Flags]
        internal enum KeyEventF
        {
            KeyDown = 0x0000,
            ExtendedKey = 0x0001,
            KeyUp = 0x0002,
            Unicode = 0x0004,
            Scancode = 0x0008,
        }

        private void SendSingleKeyboardInput(short keyCode, uint keyStatus)
        {
            var inputShift = new INPUT
            {
                type = INPUTTYPE.INPUT_KEYBOARD,
                data = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = keyCode,
                        dwFlags = keyStatus,

                        // Any keyevent with the extraInfo set to this value will be ignored by the keyboard hook and sent to the system instead.
                        dwExtraInfo = ignoreKeyEventFlag,
                    },
                },
            };

            INPUT[] inputs = [inputShift];

            _ = SendInput(1, inputs, INPUT.Size);
        }

        public static void SimulateKeyDown(ushort keyCode)
        {
            SendKey(keyCode, false);
        }

        public static void SimulateKeyUp(ushort keyCode)
        {
            SendKey(keyCode, true);
        }

        public static void SimulateKeyPress(ushort keyCode)
        {
            SendKey(keyCode, false);
            SendKey(keyCode, true);
        }

        public static void SimulateShortcut(params ushort[] keyCodes)
        {
            // 1. 先按下所有按键（顺序）
            foreach (var key in keyCodes)
            {
                SimulateKeyDown(key);
            }

            // 2. 再松开所有按键（逆序）
            for (int i = keyCodes.Length - 1; i >= 0; i--)
            {
                SimulateKeyUp(keyCodes[i]);
            }
        }

        public static void SendKey(ushort keyCode, bool keyUp)
        {
            var inputShift = new INPUT
            {
                type = INPUTTYPE.INPUT_KEYBOARD,
                data = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = (short)keyCode,
                        dwFlags = (uint)(keyUp ? KeyEventF.KeyUp : 0),
                        dwExtraInfo = (uint)IntPtr.Zero,
                    },
                },
            };

            INPUT[] inputs = [inputShift];

            _ = SendInput(1, inputs, INPUT.Size);
        }
    }
}
