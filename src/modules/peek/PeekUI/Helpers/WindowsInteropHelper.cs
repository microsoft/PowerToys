using System;
using System.Runtime.InteropServices;

namespace PeekUI.Helpers
{
    public static class WindowsInteropHelper
    {
        [StructLayout(LayoutKind.Sequential)]
        internal struct INPUT
        {
            public INPUTTYPE Type;
            public InputUnion Data;

            public static int Size
            {
                get { return Marshal.SizeOf(typeof(INPUT)); }
            }
        }

        [StructLayout(LayoutKind.Explicit)]
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
        internal struct KEYBDINPUT
        {
            internal short wVk;
            internal short wScan;
            internal uint dwFlags;
            internal int time;
            internal UIntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct HARDWAREINPUT
        {
            internal int uMsg;
            internal short wParamL;
            internal short wParamH;
        }

        internal enum INPUTTYPE : uint
        {
            INPUTMOUSE = 0,
            INPUTKEYBOARD = 1,
            INPUTHARDWARE = 2,
        }
    }
}
