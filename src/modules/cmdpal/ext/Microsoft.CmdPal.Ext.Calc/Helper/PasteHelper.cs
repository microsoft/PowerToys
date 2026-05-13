// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.System;

namespace Microsoft.CmdPal.Ext.Calc.Helper;

internal static partial class PasteHelper
{
    private const nuint IgnoreKeyEventFlag = 0x5555;

    private static void SendSingleKeyboardInput(VirtualKey keyCode, KeyEventF keyStatus)
    {
        var input = new INPUT
        {
            type = INPUTTYPE.INPUT_KEYBOARD,
            data = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = (short)keyCode,
                    dwFlags = (uint)keyStatus,

                    // Any key event with the extraInfo set to this value will be ignored
                    // by the keyboard hook and sent to the system instead.
                    dwExtraInfo = IgnoreKeyEventFlag,
                },
            },
        };

        Span<INPUT> inputs = [input];
        _ = SendInput(1, inputs, INPUT.Size);
    }

    private static bool IsKeyDown(VirtualKey key) => (GetAsyncKeyState((int)key) & 0x8000) != 0;

    private static void ReleaseModifierIfPressed(VirtualKey key)
    {
        if (IsKeyDown(key))
        {
            SendSingleKeyboardInput(key, KeyEventF.KeyUp);
        }
    }

    internal static void SendPasteKeyCombination()
    {
        ExtensionHost.LogMessage(new LogMessage { Message = "Sending paste keys..." });

        // Only release modifier keys that are actually pressed
        ReleaseModifierIfPressed(VirtualKey.LeftControl);
        ReleaseModifierIfPressed(VirtualKey.RightControl);
        ReleaseModifierIfPressed(VirtualKey.LeftWindows);
        ReleaseModifierIfPressed(VirtualKey.RightWindows);
        ReleaseModifierIfPressed(VirtualKey.LeftShift);
        ReleaseModifierIfPressed(VirtualKey.RightShift);
        ReleaseModifierIfPressed(VirtualKey.LeftMenu);
        ReleaseModifierIfPressed(VirtualKey.RightMenu);

        // Send Ctrl + V
        SendSingleKeyboardInput(VirtualKey.Control, KeyEventF.KeyDown);
        SendSingleKeyboardInput(VirtualKey.V, KeyEventF.KeyDown);
        SendSingleKeyboardInput(VirtualKey.V, KeyEventF.KeyUp);
        SendSingleKeyboardInput(VirtualKey.Control, KeyEventF.KeyUp);

        ExtensionHost.LogMessage(new LogMessage { Message = "Paste sent" });
    }

    [LibraryImport("user32.dll")]
    private static partial uint SendInput(uint nInputs, Span<INPUT> pInputs, int cbSize);

    [LibraryImport("user32.dll")]
    private static partial short GetAsyncKeyState(int vKey);

    [StructLayout(LayoutKind.Sequential)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1307:Accessible fields should begin with upper-case letter", Justification = "Matching Native Structure")]
    private struct INPUT
    {
        public INPUTTYPE type;
        public InputUnion data;

        public static int Size => Marshal.SizeOf<INPUT>();
    }

    [StructLayout(LayoutKind.Explicit)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1307:Accessible fields should begin with upper-case letter", Justification = "Matching Native Structure")]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public MOUSEINPUT mi;

        [FieldOffset(0)]
        public KEYBDINPUT ki;

        [FieldOffset(0)]
        public HARDWAREINPUT hi;
    }

    [StructLayout(LayoutKind.Sequential)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1307:Accessible fields should begin with upper-case letter", Justification = "Matching Native Structure")]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public int mouseData;
        public uint dwFlags;
        public uint time;
        public nuint dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1307:Accessible fields should begin with upper-case letter", Justification = "Matching Native Structure")]
    private struct KEYBDINPUT
    {
        public short wVk;
        public short wScan;
        public uint dwFlags;
        public int time;
        public nuint dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1307:Accessible fields should begin with upper-case letter", Justification = "Matching Native Structure")]
    private struct HARDWAREINPUT
    {
        public int uMsg;
        public short wParamL;
        public short wParamH;
    }

    private enum INPUTTYPE : uint
    {
        INPUT_MOUSE = 0,
        INPUT_KEYBOARD = 1,
        INPUT_HARDWARE = 2,
    }

    [Flags]
    private enum KeyEventF : uint
    {
        KeyDown = 0x0000,
        ExtendedKey = 0x0001,
        KeyUp = 0x0002,
        Unicode = 0x0004,
        Scancode = 0x0008,
    }
}
