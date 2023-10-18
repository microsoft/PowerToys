// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// <summary>
//     Virtual key constants.
// </summary>
// <history>
//     2008 created by Truong Do (ductdo).
//     2009-... modified by Truong Do (TruongDo).
//     2023- Included in PowerToys.
// </history>
using System;

namespace MouseWithoutBorders
{
    internal enum VK : ushort
    {
        CAPITAL = 0x14,
        NUMLOCK = 0x90,
        SHIFT = 0x10,
        CONTROL = 0x11,
        MENU = 0x12,
        ESCAPE = 0x1B,
        BACK = 0x08,
        TAB = 0x09,
        RETURN = 0x0D,
        PRIOR = 0x21,
        NEXT = 0x22,
        END = 0x23,
        HOME = 0x24,
        LEFT = 0x25,
        UP = 0x26,
        RIGHT = 0x27,
        DOWN = 0x28,
        SELECT = 0x29,
        PRINT = 0x2A,
        EXECUTE = 0x2B,
        SNAPSHOT = 0x2C,
        INSERT = 0x2D,
        DELETE = 0x2E,
        HELP = 0x2F,
        NUMPAD0 = 0x60,
        NUMPAD1 = 0x61,
        NUMPAD2 = 0x62,
        NUMPAD3 = 0x63,
        NUMPAD4 = 0x64,
        NUMPAD5 = 0x65,
        NUMPAD6 = 0x66,
        NUMPAD7 = 0x67,
        NUMPAD8 = 0x68,
        NUMPAD9 = 0x69,
        MULTIPLY = 0x6A,
        ADD = 0x6B,
        SEPARATOR = 0x6C,
        SUBTRACT = 0x6D,
        DECIMAL = 0x6E,
        DIVIDE = 0x6F,
        F1 = 0x70,
        F2 = 0x71,
        F3 = 0x72,
        F4 = 0x73,
        F5 = 0x74,
        F6 = 0x75,
        F7 = 0x76,
        F8 = 0x77,
        F9 = 0x78,
        F10 = 0x79,
        F11 = 0x7A,
        F12 = 0x7B,
        OEM_1 = 0xBA,
        OEM_PLUS = 0xBB,
        OEM_COMMA = 0xBC,
        OEM_MINUS = 0xBD,
        OEM_PERIOD = 0xBE,
        OEM_2 = 0xBF,
        OEM_3 = 0xC0,
        MEDIA_NEXT_TRACK = 0xB0,
        MEDIA_PREV_TRACK = 0xB1,
        MEDIA_STOP = 0xB2,
        MEDIA_PLAY_PAUSE = 0xB3,
        LWIN = 0x5B,
        RWIN = 0x5C,
        LSHIFT = 0xA0,
        RSHIFT = 0xA1,
        LCONTROL = 0xA2,
        RCONTROL = 0xA3,
        LMENU = 0xA4,
        RMENU = 0xA5,
    }

    internal partial class Common
    {
        internal const ushort KEYEVENTF_KEYDOWN = 0x0001;
        internal const ushort KEYEVENTF_KEYUP = 0x0002;

        internal const int WH_MOUSE = 7;
        internal const int WH_KEYBOARD = 2;
        internal const int WH_MOUSE_LL = 14;
        internal const int WH_KEYBOARD_LL = 13;

        internal const int WM_MOUSEMOVE = 0x200;
        internal const int WM_LBUTTONDOWN = 0x201;
        internal const int WM_RBUTTONDOWN = 0x204;
        internal const int WM_MBUTTONDOWN = 0x207;
        internal const int WM_XBUTTONDOWN = 0x20B;
        internal const int WM_LBUTTONUP = 0x202;
        internal const int WM_RBUTTONUP = 0x205;
        internal const int WM_MBUTTONUP = 0x208;
        internal const int WM_XBUTTONUP = 0x20C;
        internal const int WM_LBUTTONDBLCLK = 0x203;
        internal const int WM_RBUTTONDBLCLK = 0x206;
        internal const int WM_MBUTTONDBLCLK = 0x209;
        internal const int WM_MOUSEWHEEL = 0x020A;

        internal const int WM_KEYDOWN = 0x100;
        internal const int WM_KEYUP = 0x101;
        internal const int WM_SYSKEYDOWN = 0x104;
        internal const int WM_SYSKEYUP = 0x105;

        [Flags]
        internal enum LLKHF
        {
            EXTENDED = 0x01,
            INJECTED = 0x10,
            ALTDOWN = 0x20,
            UP = 0x80,
        }
    }
}
