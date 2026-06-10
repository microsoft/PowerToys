// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using FormsSendKeys = System.Windows.Forms.SendKeys;

namespace Microsoft.PowerToys.UITest.Next;

/// <summary>Virtual-key constants used by <see cref="KeyboardHelper"/>.</summary>
public enum Key : byte
{
    Ctrl = 0x11,
    Shift = 0x10,
    Alt = 0x12,
    LWin = 0x5B,
    Tab = 0x09,
    Esc = 0x1B,
    Enter = 0x0D,
    Space = 0x20,
    Backspace = 0x08,
    Delete = 0x2E,

    A = 0x41,
    B = 0x42,
    C = 0x43,
    D = 0x44,
    E = 0x45,
    F = 0x46,
    G = 0x47,
    H = 0x48,
    I = 0x49,
    J = 0x4A,
    K = 0x4B,
    L = 0x4C,
    M = 0x4D,
    N = 0x4E,
    O = 0x4F,
    P = 0x50,
    Q = 0x51,
    R = 0x52,
    S = 0x53,
    T = 0x54,
    U = 0x55,
    V = 0x56,
    W = 0x57,
    X = 0x58,
    Y = 0x59,
    Z = 0x5A,
}

/// <summary>
/// Global keyboard input. Uses the same hybrid strategy as the legacy harness because pure
/// <c>keybd_event</c> injection doesn't reliably trigger <c>RegisterHotKey</c>-registered global
/// hotkeys for the PowerToys runner: hold LWIN down via <c>keybd_event</c>, then send the
/// remaining chord via <see cref="System.Windows.Forms.SendKeys.SendWait"/> which uses
/// SendInput with proper modifier tracking, then release LWIN.
/// </summary>
public static class KeyboardHelper
{
    [DllImport("user32.dll", SetLastError = true)]
#pragma warning disable SA1300 // win32 API name
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
#pragma warning restore SA1300

    private const uint KEYEVENTF_KEYUP = 0x2;
    private const byte VK_LWIN = 0x5B;

    /// <summary>
    /// Send a chord of keys. If the chord contains <see cref="Key.LWin"/>, LWIN is held via
    /// <c>keybd_event</c> while the remaining keys are sent via <see cref="FormsSendKeys.SendWait"/>.
    /// Otherwise everything goes through SendKeys.SendWait (the modifier-aware Windows path).
    /// </summary>
    public static void SendKeys(params Key[] keys)
    {
        bool winDown = false;
        var chord = new System.Text.StringBuilder();

        foreach (var k in keys)
        {
            switch (k)
            {
                case Key.LWin:
                    keybd_event(VK_LWIN, 0, 0, UIntPtr.Zero);
                    winDown = true;
                    break;
                case Key.Ctrl: chord.Append('^'); break;
                case Key.Shift: chord.Append('+'); break;
                case Key.Alt: chord.Append('%'); break;
                case Key.Esc: chord.Append("{ESC}"); break;
                case Key.Enter: chord.Append("{ENTER}"); break;
                case Key.Tab: chord.Append("{TAB}"); break;
                case Key.Space: chord.Append(' '); break;
                case Key.Backspace: chord.Append("{BACKSPACE}"); break;
                case Key.Delete: chord.Append("{DELETE}"); break;
                default:
                    // Letter / digit keys map to their lowercase character for SendKeys.
                    chord.Append(((char)k).ToString().ToLowerInvariant());
                    break;
            }
        }

        try
        {
            if (chord.Length > 0)
            {
                FormsSendKeys.SendWait(chord.ToString());
            }
        }
        finally
        {
            if (winDown)
            {
                keybd_event(VK_LWIN, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            }
        }
    }
}
