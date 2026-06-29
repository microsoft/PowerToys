// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.System;

namespace Microsoft.CmdPal.Ext.Power.Helpers;

internal static partial class EnergySaverFallbackHelper
{
    internal const string PowerSettingsUri = "ms-settings:powersleep";

    internal static bool TryOpenQuickSettings()
    {
        try
        {
            ReleaseModifierIfPressed(VirtualKey.LeftControl);
            ReleaseModifierIfPressed(VirtualKey.RightControl);
            ReleaseModifierIfPressed(VirtualKey.LeftWindows);
            ReleaseModifierIfPressed(VirtualKey.RightWindows);
            ReleaseModifierIfPressed(VirtualKey.LeftShift);
            ReleaseModifierIfPressed(VirtualKey.RightShift);
            ReleaseModifierIfPressed(VirtualKey.LeftMenu);
            ReleaseModifierIfPressed(VirtualKey.RightMenu);

            SendSingleKeyboardInput(VirtualKey.LeftWindows, KeyEventF.KeyDown);
            SendSingleKeyboardInput(VirtualKey.A, KeyEventF.KeyDown);
            SendSingleKeyboardInput(VirtualKey.A, KeyEventF.KeyUp);
            SendSingleKeyboardInput(VirtualKey.LeftWindows, KeyEventF.KeyUp);
            return true;
        }
        catch
        {
            return false;
        }
    }

    internal static bool TryOpenPowerSettings() =>
        ShellHelpers.OpenInShell(PowerSettingsUri);

    private static void ReleaseModifierIfPressed(VirtualKey key)
    {
        if (IsKeyDown(key))
        {
            SendSingleKeyboardInput(key, KeyEventF.KeyUp);
        }
    }

    private static bool IsKeyDown(VirtualKey key) => (GetAsyncKeyState((int)key) & 0x8000) != 0;

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
                    dwExtraInfo = IgnoreKeyEventFlag,
                },
            },
        };

        Span<INPUT> inputs = [input];
        _ = SendInput(1, inputs, INPUT.Size);
    }

    private static readonly nuint IgnoreKeyEventFlag = 0x5555;

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
        public KEYBDINPUT ki;
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

    private enum INPUTTYPE : uint
    {
        INPUT_KEYBOARD = 1,
    }

    [Flags]
    private enum KeyEventF : uint
    {
        KeyDown = 0x0000,
        KeyUp = 0x0002,
    }
}
