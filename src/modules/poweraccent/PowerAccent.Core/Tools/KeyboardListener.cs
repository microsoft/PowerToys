using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PowerAccent.Core.Tools;

internal class KeyboardListener : IDisposable
{
    /// <summary>
    /// Creates global keyboard listener.
    /// </summary>
    public KeyboardListener()
    {
        // We have to store the LowLevelKeyboardProc, so that it is not garbage collected runtime
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
    /// <param name="character">Character</param>
    /// <param name="keyEvent">Keyboard event</param>
    /// <param name="vkCode">VKCode</param>
    private delegate bool KeyboardCallbackAsync(InterceptKeys.KeyEvent keyEvent, int vkCode, string character);

    /// <summary>
    /// Actual callback hook.
    /// 
    /// <remarks>Calls asynchronously the asyncCallback.</remarks>
    /// </summary>
    /// <param name="nCode"></param>
    /// <param name="wParam"></param>
    /// <param name="lParam"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private IntPtr LowLevelKeyboardProc(int nCode, UIntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
            if (wParam.ToUInt32() == (int)InterceptKeys.KeyEvent.WM_KEYDOWN ||
                wParam.ToUInt32() == (int)InterceptKeys.KeyEvent.WM_KEYUP ||
                wParam.ToUInt32() == (int)InterceptKeys.KeyEvent.WM_SYSKEYDOWN ||
                wParam.ToUInt32() == (int)InterceptKeys.KeyEvent.WM_SYSKEYUP)
            {
                //Captures the character(s) pressed only on WM_KEYDOWN
                var chars = InterceptKeys.VKCodeToString((uint)Marshal.ReadInt32(lParam),
                    wParam.ToUInt32() == (int)InterceptKeys.KeyEvent.WM_KEYDOWN ||
                     wParam.ToUInt32() == (int)InterceptKeys.KeyEvent.WM_SYSKEYDOWN);

                if (!hookedKeyboardCallbackAsync.Invoke((InterceptKeys.KeyEvent)wParam.ToUInt32(), Marshal.ReadInt32(lParam), chars))
                {
                    return (IntPtr)1;
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
    bool KeyboardListener_KeyboardCallbackAsync(InterceptKeys.KeyEvent keyEvent, int vkCode, string character)
    {
        switch (keyEvent)
        {
            // KeyDown events
            case InterceptKeys.KeyEvent.WM_KEYDOWN:
                if (KeyDown != null)
                    return KeyDown.Invoke(this, new RawKeyEventArgs(vkCode, false, character));
                break;
            case InterceptKeys.KeyEvent.WM_SYSKEYDOWN:
                if (KeyDown != null)
                    return KeyDown.Invoke(this, new RawKeyEventArgs(vkCode, true, character));
                break;

            // KeyUp events
            case InterceptKeys.KeyEvent.WM_KEYUP:
                if (KeyUp != null)
                    return KeyUp.Invoke(this, new RawKeyEventArgs(vkCode, false, character));
                break;
            case InterceptKeys.KeyEvent.WM_SYSKEYUP:
                if (KeyUp != null)
                    return KeyUp.Invoke(this, new RawKeyEventArgs(vkCode, true, character));
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
        /// VKCode of the key.
        /// </summary>
        public int VKCode;

        /// <summary>
        /// WPF Key of the key.
        /// </summary>
        public uint Key;

        /// <summary>
        /// Is the hitted key system key.
        /// </summary>
        public bool IsSysKey;

        /// <summary>
        /// Convert to string.
        /// </summary>
        /// <returns>Returns string representation of this key, if not possible empty string is returned.</returns>
        public override string ToString()
        {
            return Character;
        }

        /// <summary>
        /// Unicode character of key pressed.
        /// </summary>
        public string Character;

        /// <summary>
        /// Create raw keyevent arguments.
        /// </summary>
        /// <param name="VKCode"></param>
        /// <param name="isSysKey"></param>
        /// <param name="Character">Character</param>
        public RawKeyEventArgs(int VKCode, bool isSysKey, string Character)
        {
            this.VKCode = VKCode;
            IsSysKey = isSysKey;
            this.Character = Character;
            Key = (uint)VKCode; //User32.MapVirtualKey((uint)VKCode, User32.MAPVK.MAPVK_VK_TO_VSC_EX);
        }

    }
}

/// <summary>
/// Winapi Key interception helper class.
/// </summary>
internal static class InterceptKeys
{
    public delegate IntPtr LowLevelKeyboardProc(int nCode, UIntPtr wParam, IntPtr lParam);
    public static int WH_KEYBOARD_LL = 13;

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
        WM_SYSKEYDOWN = 260
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

    #region Convert VKCode to string
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
    private static bool lastIsDead;

    /// <summary>
    /// Convert VKCode to Unicode.
    /// <remarks>isKeyDown is required for because of keyboard state inconsistencies!</remarks>
    /// </summary>
    /// <param name="VKCode">VKCode</param>
    /// <param name="isKeyDown">Is the key down event?</param>
    /// <returns>String representing single unicode character.</returns>
    public static string VKCodeToString(uint VKCode, bool isKeyDown)
    {
        // ToUnicodeEx needs StringBuilder, it populates that during execution.
        System.Text.StringBuilder sbString = new System.Text.StringBuilder(5);

        byte[] bKeyState = new byte[255];
        bool bKeyStateStatus;
        bool isDead = false;

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
            return "";

        // Gets the layout of keyboard
        IntPtr HKL = GetKeyboardLayout(currentWindowThreadID);

        // Maps the virtual keycode
        uint lScanCode = MapVirtualKeyEx(VKCode, 0, HKL);

        // Keyboard state goes inconsistent if this is not in place. In other words, we need to call above commands in UP events also.
        if (!isKeyDown)
            return "";

        // Converts the VKCode to unicode
        int relevantKeyCountInBuffer = ToUnicodeEx(VKCode, lScanCode, bKeyState, sbString, sbString.Capacity, 0, HKL);

        string ret = "";

        switch (relevantKeyCountInBuffer)
        {
            // Dead keys (^,`...)
            case -1:
                isDead = true;

                // We must clear the buffer because ToUnicodeEx messed it up, see below.
                ClearKeyboardBuffer(VKCode, lScanCode, HKL);
                break;

            case 0:
                break;

            // Single character in buffer
            case 1:
                ret = sbString.Length == 0 ? "" : sbString[0].ToString();
                break;

            // Two or more (only two of them is relevant)
            case 2:
            default:
                ret = sbString.ToString().Substring(0, 2);
                break;
        }

        // We inject the last dead key back, since ToUnicodeEx removed it.
        // More about this peculiar behavior see e.g: 
        //   http://www.experts-exchange.com/Programming/System/Windows__Programming/Q_23453780.html
        //   http://blogs.msdn.com/michkap/archive/2005/01/19/355870.aspx
        //   http://blogs.msdn.com/michkap/archive/2007/10/27/5717859.aspx
        if (lastVKCode != 0 && lastIsDead)
        {
            System.Text.StringBuilder sbTemp = new System.Text.StringBuilder(5);
            _ = ToUnicodeEx(lastVKCode, lastScanCode, lastKeyState, sbTemp, sbTemp.Capacity, 0, HKL);
            lastVKCode = 0;

            return ret;
        }

        // Save these
        lastScanCode = lScanCode;
        lastVKCode = VKCode;
        lastIsDead = isDead;
        lastKeyState = (byte[])bKeyState.Clone();

        return ret;
    }

    private static void ClearKeyboardBuffer(uint vk, uint sc, IntPtr hkl)
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder(10);

        int rc;
        do
        {
            byte[] lpKeyStateNull = new byte[255];
            rc = ToUnicodeEx(vk, sc, lpKeyStateNull, sb, sb.Capacity, 0, hkl);
        } while (rc < 0);
    }
    #endregion
}
