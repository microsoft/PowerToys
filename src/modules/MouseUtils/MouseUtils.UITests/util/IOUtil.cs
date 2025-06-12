// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

// The MouseUtils module relies on simulating system-level input events (such as mouse movement or key presses) to test visual or behavioral responses.
// The UI Test framework provides built-in methods for simulating mouse movement and clicks, which work for MouseUtils reliably on high-performance dev boxes.
// However, on low-performance environments such as CI/CD pipelines or virtual machines, these simulated input events are not always correctly recognized by the operating system.
// IOUtils class is added specifically for MouseUtils tests.
// For any test scenario that involves simulating continuous mouse movement (e.g., detecting crosshair changes while moving the cursor),
// input simulation methods in IOUtils class should be used.
namespace MouseUtils.UITests
{
    public class IOUtil
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

        [Flags]
        internal enum MouseEventF : uint
        {
            MOVE = 0x0001,
            LEFTDOWN = 0x0002,
            LEFTUP = 0x0004,
            RIGHTDOWN = 0x0008,
            RIGHTUP = 0x0010,
            ABSOLUTE = 0x8000,
            MIDDLEDOWN = 0x0020,
            MIDDLEUP = 0x0040,
        }

        public static void SimulateMouseDown(bool leftButton = true)
        {
            SendMouseInput(leftButton ? MouseEventF.LEFTDOWN : MouseEventF.RIGHTDOWN);
        }

        public static void SimulateMouseUp(bool leftButton = true)
        {
            SendMouseInput(leftButton ? MouseEventF.LEFTUP : MouseEventF.RIGHTUP);
        }

        public static void MouseClick(bool leftButton = true)
        {
            SendMouseInput(leftButton ? MouseEventF.LEFTDOWN : MouseEventF.RIGHTDOWN);
            SendMouseInput(leftButton ? MouseEventF.LEFTUP : MouseEventF.RIGHTUP);
        }

        private static void SendMouseInput(MouseEventF mouseFlags)
        {
            var input = new INPUT
            {
                type = INPUTTYPE.INPUT_MOUSE,
                data = new InputUnion
                {
                    mi = new MOUSEINPUT
                    {
                        dx = 0,
                        dy = 0,
                        mouseData = 0,
                        dwFlags = (uint)mouseFlags,
                        time = 0,
                        dwExtraInfo = UIntPtr.Zero,
                    },
                },
            };

            INPUT[] inputs = [input];
            _ = SendInput(1, inputs, INPUT.Size);
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetMessageExtraInfo();

        public static void MoveMouseBy(int dx, int dy)
        {
            var input = new INPUT
            {
                type = INPUTTYPE.INPUT_MOUSE,
                data = new InputUnion
                {
                    mi = new MOUSEINPUT
                    {
                        dx = dx,
                        dy = dy,
                        mouseData = 0,
                        dwFlags = (uint)MouseEventF.MOVE,
                        time = 0,
                        dwExtraInfo = (nuint)GetMessageExtraInfo(),
                    },
                },
            };

            INPUT[] inputs = [input];
            _ = SendInput(1, inputs, INPUT.Size);
        }

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        public static void MoveMouseTo(int x, int y)
        {
            int screenWidth = GetSystemMetrics(0);
            int screenHeight = GetSystemMetrics(1);

            int normalizedX = (int)(x * 65535 / screenWidth);
            int normalizedY = (int)(y * 65535 / screenHeight);

            var input = new INPUT
            {
                type = INPUTTYPE.INPUT_MOUSE,
                data = new InputUnion
                {
                    mi = new MOUSEINPUT
                    {
                        dx = normalizedX,
                        dy = normalizedY,
                        mouseData = 0,
                        dwFlags = (uint)(MouseEventF.MOVE | MouseEventF.ABSOLUTE),
                        time = 0,
                        dwExtraInfo = UIntPtr.Zero,
                    },
                },
            };

            INPUT[] inputs = [input];
            _ = SendInput(1, inputs, INPUT.Size);
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
            foreach (var key in keyCodes)
            {
                SimulateKeyDown(key);
            }

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
