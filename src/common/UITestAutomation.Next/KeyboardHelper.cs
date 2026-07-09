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
    Insert = 0x2D,
    Home = 0x24,
    End = 0x23,
    PageUp = 0x21,
    PageDown = 0x22,
    Left = 0x25,
    Up = 0x26,
    Right = 0x27,
    Down = 0x28,

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

    Num0 = 0x30,
    Num1 = 0x31,
    Num2 = 0x32,
    Num3 = 0x33,
    Num4 = 0x34,
    Num5 = 0x35,
    Num6 = 0x36,
    Num7 = 0x37,
    Num8 = 0x38,
    Num9 = 0x39,

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
    private const uint KEYEVENTF_EXTENDEDKEY = 0x1;
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
                case Key.Insert: chord.Append("{INSERT}"); break;
                case Key.Home: chord.Append("{HOME}"); break;
                case Key.End: chord.Append("{END}"); break;
                case Key.PageUp: chord.Append("{PGUP}"); break;
                case Key.PageDown: chord.Append("{PGDN}"); break;
                case Key.Up: chord.Append("{UP}"); break;
                case Key.Down: chord.Append("{DOWN}"); break;
                case Key.Left: chord.Append("{LEFT}"); break;
                case Key.Right: chord.Append("{RIGHT}"); break;
                case Key.F1: chord.Append("{F1}"); break;
                case Key.F2: chord.Append("{F2}"); break;
                case Key.F3: chord.Append("{F3}"); break;
                case Key.F4: chord.Append("{F4}"); break;
                case Key.F5: chord.Append("{F5}"); break;
                case Key.F6: chord.Append("{F6}"); break;
                case Key.F7: chord.Append("{F7}"); break;
                case Key.F8: chord.Append("{F8}"); break;
                case Key.F9: chord.Append("{F9}"); break;
                case Key.F10: chord.Append("{F10}"); break;
                case Key.F11: chord.Append("{F11}"); break;
                case Key.F12: chord.Append("{F12}"); break;
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

    /// <summary>Press (and hold) a key via <c>keybd_event</c>. Pair with <see cref="ReleaseKey"/>.</summary>
    public static void PressKey(Key key) =>
        keybd_event((byte)key, 0, IsExtended(key) ? KEYEVENTF_EXTENDEDKEY : 0u, UIntPtr.Zero);

    /// <summary>Release a key previously pressed with <see cref="PressKey"/>.</summary>
    public static void ReleaseKey(Key key) =>
        keybd_event((byte)key, 0, KEYEVENTF_KEYUP | (IsExtended(key) ? KEYEVENTF_EXTENDEDKEY : 0u), UIntPtr.Zero);

    /// <summary>Press + release a single key.</summary>
    public static void SendKey(Key key)
    {
        PressKey(key);
        Thread.Sleep(20);
        ReleaseKey(key);
    }

    /// <summary>Press + release each key in order (independent taps, not a held chord).</summary>
    public static void SendKeySequence(params Key[] keys)
    {
        foreach (var k in keys)
        {
            SendKey(k);
            Thread.Sleep(20);
        }
    }

    private static bool IsExtended(Key key) => key is
        Key.Left or Key.Up or Key.Right or Key.Down or
        Key.Home or Key.End or Key.PageUp or Key.PageDown or
        Key.Insert or Key.Delete;
}
