// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
#pragma warning disable SA1310 // FieldNamesMustNotContainUnderscore

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PowerAccent.Core.Tools;

internal class KeyboardListener : IDisposable
{
    /// <summary>
    /// Initializes a new instance of the <see cref="KeyboardListener"/> class.
    /// Creates global keyboard listener.
    /// </summary>
    public KeyboardListener()
    {
        // We have to store the LowLevelKeyboardProc, so that it is not garbage collected by runtime
        _hookedLowLevelKeyboardProc = LowLevelKeyboardProc;

        // Set the hook
        _hookId = InterceptKeys.SetHook(_hookedLowLevelKeyboardProc);

        // Assign the asynchronous callback event
        hookedKeyboardCallbackAsync = new KeyboardCallbackAsync(KeyboardListener_KeyboardCallbackAsync);
    }

    /// <summary>
    /// Fired when any of the keys is pressed down.
    /// </summary>
    public event RawKeyEventHandler KeyDown;

    /// <summary>
    /// Fired when any of the keys is released.
    /// </summary>
    public event RawKeyEventHandler KeyUp;

    /// <summary>
    /// Hook ID
    /// </summary>
    private readonly IntPtr _hookId = IntPtr.Zero;

    /// <summary>
    /// Contains the hooked callback in runtime.
    /// </summary>
    private readonly InterceptKeys.LowLevelKeyboardProc _hookedLowLevelKeyboardProc;

    /// <summary>
    /// Event to be invoked asynchronously (BeginInvoke) each time key is pressed.
    /// </summary>
    private KeyboardCallbackAsync hookedKeyboardCallbackAsync;

    /// <summary>
    /// Raw keyevent handler.
    /// </summary>
    /// <param name="sender">sender</param>
    /// <param name="args">raw keyevent arguments</param>
    public delegate bool RawKeyEventHandler(object sender, RawKeyEventArgs args);

    /// <summary>
    /// Asynchronous callback hook.
    /// </summary>
    /// <param name="keyEvent">Keyboard event</param>
    /// <param name="vkCode">VKCode</param>
    /// <param name="character">Character</param>
    private delegate bool KeyboardCallbackAsync(InterceptKeys.KeyEvent keyEvent, int vkCode, string character);

    /// <summary>
    /// Actual callback hook.
    /// <remarks>Calls asynchronously the asyncCallback.</remarks>
    /// </summary>
    /// <param name="nCode">VKCode</param>
    /// <param name="wParam">wParam</param>
    /// <param name="lParam">lParam</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private IntPtr LowLevelKeyboardProc(int nCode, UIntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            if (wParam.ToUInt32() == (int)InterceptKeys.KeyEvent.WM_KEYDOWN ||
                wParam.ToUInt32() == (int)InterceptKeys.KeyEvent.WM_KEYUP)
            {
                // Captures the character(s) pressed only on WM_KEYDOWN
                var chars = InterceptKeys.VKCodeToString(
                    (uint)Marshal.ReadInt32(lParam),
                    wParam.ToUInt32() == (int)InterceptKeys.KeyEvent.WM_KEYDOWN);

                if (!hookedKeyboardCallbackAsync.Invoke((InterceptKeys.KeyEvent)wParam.ToUInt32(), Marshal.ReadInt32(lParam), chars))
                {
                    return (IntPtr)1;
                }
            }
        }

        return InterceptKeys.CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    /// <summary>
    /// HookCallbackAsync procedure that calls accordingly the KeyDown or KeyUp events.
    /// </summary>
    /// <param name="keyEvent">Keyboard event</param>
    /// <param name="vkCode">VKCode</param>
    /// <param name="character">Character as string.</param>
    private bool KeyboardListener_KeyboardCallbackAsync(InterceptKeys.KeyEvent keyEvent, int vkCode, string character)
    {
        switch (keyEvent)
        {
            // KeyDown events
            case InterceptKeys.KeyEvent.WM_KEYDOWN:
                if (KeyDown != null)
                {
                    return KeyDown.Invoke(this, new RawKeyEventArgs(vkCode, character));
                }

                break;

            // KeyUp events
            case InterceptKeys.KeyEvent.WM_KEYUP:
                if (KeyUp != null)
                {
                    return KeyUp.Invoke(this, new RawKeyEventArgs(vkCode, character));
                }

                break;
            default:
                break;
        }

        return true;
    }

    public void Dispose()
    {
        InterceptKeys.UnhookWindowsHookEx(_hookId);
    }

    /// <summary>
    /// Raw KeyEvent arguments.
    /// </summary>
    public class RawKeyEventArgs : EventArgs
    {
        /// <summary>
        /// WPF Key of the key.
        /// </summary>
#pragma warning disable SA1401 // Fields should be private
        public uint Key;
#pragma warning restore SA1401 // Fields should be private

        /// <summary>
        /// Convert to string.
        /// </summary>
        /// <returns>Returns string representation of this key, if not possible empty string is returned.</returns>
        public override string ToString()
        {
            return character;
        }

        /// <summary>
        /// Unicode character of key pressed.
        /// </summary>
        private string character;

        /// <summary>
        /// Initializes a new instance of the <see cref="RawKeyEventArgs"/> class.
        /// Create raw keyevent arguments.
        /// </summary>
        /// <param name="vKCode">VKCode</param>
        /// <param name="character">Character</param>
        public RawKeyEventArgs(int vKCode, string character)
        {
            this.character = character;
            Key = (uint)vKCode; // User32.MapVirtualKey((uint)VKCode, User32.MAPVK.MAPVK_VK_TO_VSC_EX);
        }
    }
}

/// <summary>
/// Winapi Key interception helper class.
/// </summary>
internal static class InterceptKeys
{
    public delegate IntPtr LowLevelKeyboardProc(int nCode, UIntPtr wParam, IntPtr lParam);

    private const int WH_KEYBOARD_LL = 13;

    /// <summary>
    /// Key event
    /// </summary>
    public enum KeyEvent : int
    {
        /// <summary>
        /// Key down
        /// </summary>
        WM_KEYDOWN = 256,

        /// <summary>
        /// Key up
        /// </summary>
        WM_KEYUP = 257,

        /// <summary>
        /// System key up
        /// </summary>
        WM_SYSKEYUP = 261,

        /// <summary>
        /// System key down
        /// </summary>
        WM_SYSKEYDOWN = 260,
    }

    public static IntPtr SetHook(LowLevelKeyboardProc proc)
    {
        using (Process curProcess = Process.GetCurrentProcess())
        using (ProcessModule curModule = curProcess.MainModule)
        {
            return SetWindowsHookEx(WH_KEYBOARD_LL, proc, (IntPtr)0, 0);
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, UIntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr GetModuleHandle(string lpModuleName);

    // Note: Sometimes single VKCode represents multiple chars, thus string.
    // E.g. typing "^1" (notice that when pressing 1 the both characters appear,
    // because of this behavior, "^" is called dead key)
    [DllImport("user32.dll")]
#pragma warning disable CA1838 // Éviter les paramètres 'StringBuilder' pour les P/Invoke
    private static extern int ToUnicodeEx(uint wVirtKey, uint wScanCode, byte[] lpKeyState, [Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pwszBuff, int cchBuff, uint wFlags, IntPtr dwhkl);
#pragma warning restore CA1838 // Éviter les paramètres 'StringBuilder' pour les P/Invoke

    [DllImport("user32.dll")]
    private static extern bool GetKeyboardState(byte[] lpKeyState);

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKeyEx(uint uCode, uint uMapType, IntPtr dwhkl);

    [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
    private static extern IntPtr GetKeyboardLayout(uint dwLayout);

    [DllImport("User32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("User32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    private static uint lastVKCode;
    private static uint lastScanCode;
    private static byte[] lastKeyState = new byte[255];

    /// <summary>
    /// Convert VKCode to Unicode.
    /// <remarks>isKeyDown is required for because of keyboard state inconsistencies!</remarks>
    /// </summary>
    /// <param name="vKCode">VKCode</param>
    /// <param name="isKeyDown">Is the key down event?</param>
    /// <returns>String representing single unicode character.</returns>
    public static string VKCodeToString(uint vKCode, bool isKeyDown)
    {
        // ToUnicodeEx needs StringBuilder, it populates that during execution.
        System.Text.StringBuilder sbString = new System.Text.StringBuilder(5);

        byte[] bKeyState = new byte[255];
        bool bKeyStateStatus;

        // Gets the current windows window handle, threadID, processID
        IntPtr currentHWnd = GetForegroundWindow();
        uint currentProcessID;
        uint currentWindowThreadID = GetWindowThreadProcessId(currentHWnd, out currentProcessID);

        // This programs Thread ID
        uint thisProgramThreadId = GetCurrentThreadId();

        // Attach to active thread so we can get that keyboard state
        if (AttachThreadInput(thisProgramThreadId, currentWindowThreadID, true))
        {
            // Current state of the modifiers in keyboard
            bKeyStateStatus = GetKeyboardState(bKeyState);

            // Detach
            AttachThreadInput(thisProgramThreadId, currentWindowThreadID, false);
        }
        else
        {
            // Could not attach, perhaps it is this process?
            bKeyStateStatus = GetKeyboardState(bKeyState);
        }

        // On failure we return empty string.
        if (!bKeyStateStatus)
        {
            return string.Empty;
        }

        // Gets the layout of keyboard
        IntPtr hkl = GetKeyboardLayout(currentWindowThreadID);

        // Maps the virtual keycode
        uint lScanCode = MapVirtualKeyEx(vKCode, 0, hkl);

        // Keyboard state goes inconsistent if this is not in place. In other words, we need to call above commands in UP events also.
        if (!isKeyDown)
        {
            return string.Empty;
        }

        // Converts the VKCode to unicode
        const uint wFlags = 1 << 2; // If bit 2 is set, keyboard state is not changed (Windows 10, version 1607 and newer)
        int relevantKeyCountInBuffer = ToUnicodeEx(vKCode, lScanCode, bKeyState, sbString, sbString.Capacity, wFlags, hkl);

        string ret = string.Empty;

        switch (relevantKeyCountInBuffer)
        {
            // dead key
            case -1:
                break;

            case 0:
                break;

            // Single character in buffer
            case 1:
                ret = sbString.Length == 0 ? string.Empty : sbString[0].ToString();
                break;

            // Two or more (only two of them is relevant)
            case 2:
            default:
                ret = sbString.ToString().Substring(0, 2);
                break;
        }

        // Save these
        lastScanCode = lScanCode;
        lastVKCode = vKCode;
        lastKeyState = (byte[])bKeyState.Clone();

        return ret;
    }
}
