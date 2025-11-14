// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

// <summary>
//     Virtual key constants.
// </summary>
// <history>
//     2008 created by Truong Do (ductdo).
//     2009-... modified by Truong Do (TruongDo).
//     2023- Included in PowerToys.
// </history>
namespace MouseWithoutBorders.Core;

internal partial class WM
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
    internal const int WM_MOUSEHWHEEL = 0x020E;

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
