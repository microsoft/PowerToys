/// KEYBOARD.CS
/// (c) 2006 by Emma Burrows
/// This file contains the following items:
///  - KeyboardHook: class to enable low-level keyboard hook using
///    the Windows API.
///  - KeyboardHookEventHandler: delegate to handle the KeyIntercepted
///    event raised by the KeyboardHook class.
///  - KeyboardHookEventArgs: EventArgs class to contain the information
///    returned by the KeyIntercepted event.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace WinAlfred.Helper
{
    /// <summary>
    /// Low-level keyboard intercept class to trap and suppress system keys.
    /// </summary>
    public class GlobalKeyboardHook : IDisposable
    {
        /// <summary>
        /// Parameters accepted by the KeyboardHook constructor.
        /// </summary>
        public enum Parameters
        {
            None,
            AllowAltTab,
            AllowWindowsKey,
            AllowAltTabAndWindows,
            PassAllKeysToNextApp
        }

        //Internal parameters
        private bool PassAllKeysToNextApp = false;
        private bool AllowAltTab = false;
        private bool AllowWindowsKey = false;

        //Keyboard API constants
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYUP = 0x0105;

        //Modifier key constants
        private const int VK_SHIFT = 0x10;
        private const int VK_CONTROL = 0x11;
        private const int VK_MENU = 0x12;
        private const int VK_CAPITAL = 0x14;

        //Variables used in the call to SetWindowsHookEx
        private HookHandlerDelegate proc;
        private IntPtr hookID = IntPtr.Zero;
        internal delegate IntPtr HookHandlerDelegate(
            int nCode, IntPtr wParam, ref KBDLLHOOKSTRUCT lParam);

        /// <summary>
        /// Event triggered when a keystroke is intercepted by the 
        /// low-level hook.
        /// </summary>
        public event KeyboardHookEventHandler KeyIntercepted;

        // Structure returned by the hook whenever a key is pressed
        internal struct KBDLLHOOKSTRUCT
        {
            public int vkCode;
            int scanCode;
            public int flags;
            int time;
            int dwExtraInfo;
        }

        #region Constructors
        /// <summary>
        /// Sets up a keyboard hook to trap all keystrokes without 
        /// passing any to other applications.
        /// </summary>
        public GlobalKeyboardHook()
        {
            proc = new HookHandlerDelegate(HookCallback);
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                hookID = NativeMethods.SetWindowsHookEx(WH_KEYBOARD_LL, proc,
                    NativeMethods.GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        /// <summary>
        /// Sets up a keyboard hook with custom parameters.
        /// </summary>
        /// <param name="param">A valid name from the Parameter enum; otherwise, the 
        /// default parameter Parameter.None will be used.</param>
        public GlobalKeyboardHook(string param)
            : this()
        {
            if (!String.IsNullOrEmpty(param) && Enum.IsDefined(typeof(Parameters), param))
            {
                SetParameters((Parameters)Enum.Parse(typeof(Parameters), param));
            }
        }

        /// <summary>
        /// Sets up a keyboard hook with custom parameters.
        /// </summary>
        /// <param name="param">A value from the Parameters enum.</param>
        public GlobalKeyboardHook(Parameters param)
            : this()
        {
            SetParameters(param);
        }
    
        private void SetParameters(Parameters param)
        {
            switch (param)
            {
                case Parameters.None:
                    break;
                case Parameters.AllowAltTab:
                    AllowAltTab = true;
                    break;
                case Parameters.AllowWindowsKey:
                    AllowWindowsKey = true;
                    break;
                case Parameters.AllowAltTabAndWindows:
                    AllowAltTab = true;
                    AllowWindowsKey = true;
                    break;
                case Parameters.PassAllKeysToNextApp:
                    PassAllKeysToNextApp = true;
                    break;
            }
        }
        #endregion

        #region Check Modifier keys
        /// <summary>
        /// Checks whether Alt, Shift, Control or CapsLock
        /// is enabled at the same time as another key.
        /// Modify the relevant sections and return type 
        /// depending on what you want to do with modifier keys.
        /// </summary>
        private void CheckModifiers()
        {
            StringBuilder sb = new StringBuilder();

            if ((NativeMethods.GetKeyState(VK_CAPITAL) & 0x0001) != 0)
            {
                //CAPSLOCK is ON
                sb.AppendLine("Capslock is enabled.");
            }

            if ((NativeMethods.GetKeyState(VK_SHIFT) & 0x8000) != 0)
            { 
                //SHIFT is pressed
                sb.AppendLine("Shift is pressed.");
            }
            if ((NativeMethods.GetKeyState(VK_CONTROL) & 0x8000) != 0)
            {
                //CONTROL is pressed
                sb.AppendLine("Control is pressed.");
            }
            if ((NativeMethods.GetKeyState(VK_MENU) & 0x8000) != 0)
            {
                //ALT is pressed
                sb.AppendLine("Alt is pressed.");
            }
            Console.WriteLine(sb.ToString());
        }
        #endregion Check Modifier keys

        #region Hook Callback Method
        /// <summary>
        /// Processes the key event captured by the hook.
        /// </summary>
        private IntPtr HookCallback(
            int nCode, IntPtr wParam, ref KBDLLHOOKSTRUCT lParam)
        {
            bool AllowKey = PassAllKeysToNextApp;

            //Filter wParam for KeyUp events only
            if (nCode >= 0)
            {
                if (wParam == (IntPtr)WM_KEYUP || wParam == (IntPtr)WM_SYSKEYUP)
                {

                    // Check for modifier keys, but only if the key being
                    // currently processed isn't a modifier key (in other
                    // words, CheckModifiers will only run if Ctrl, Shift,
                    // CapsLock or Alt are active at the same time as
                    // another key)
                    if (!(lParam.vkCode >= 160 && lParam.vkCode <= 164))
                    {
                        CheckModifiers();
                    }

                    // Check for key combinations that are allowed to 
                    // get through to Windows
                    //
                    // Ctrl+Esc or Windows key
                    if (AllowWindowsKey)
                    {
                        switch (lParam.flags)
                        {
                                //Ctrl+Esc
                            case 0:
                                if (lParam.vkCode == 27)
                                    AllowKey = true;
                                break;

                                //Windows keys
                            case 1:
                                if ((lParam.vkCode == 91) || (lParam.vkCode == 92))
                                    AllowKey = true;
                                break;
                        }
                    }
                    // Alt+Tab
                    if (AllowAltTab)
                    {
                        if ((lParam.flags == 32) && (lParam.vkCode == 9))
                            AllowKey = true;
                    }

                    OnKeyIntercepted(new KeyboardHookEventArgs(lParam.vkCode, AllowKey));
                }

                //If this key is being suppressed, return a dummy value
                if (AllowKey == false)
                    return (System.IntPtr)1;
            }
            //Pass key to next application
            return NativeMethods.CallNextHookEx(hookID, nCode, wParam, ref lParam);

        }
        #endregion

        #region Event Handling
        /// <summary>
        /// Raises the KeyIntercepted event.
        /// </summary>
        /// <param name="e">An instance of KeyboardHookEventArgs</param>
        public void OnKeyIntercepted(KeyboardHookEventArgs e)
        {
            if (KeyIntercepted != null)
                KeyIntercepted(e);
        }

        /// <summary>
        /// Delegate for KeyboardHook event handling.
        /// </summary>
        /// <param name="e">An instance of InterceptKeysEventArgs.</param>
        public delegate void KeyboardHookEventHandler(KeyboardHookEventArgs e);

        /// <summary>
        /// Event arguments for the KeyboardHook class's KeyIntercepted event.
        /// </summary>
        public class KeyboardHookEventArgs : System.EventArgs
        {

            private string keyName;
            private int keyCode;
            private bool passThrough;

            /// <summary>
            /// The name of the key that was pressed.
            /// </summary>
            public string KeyName
            {
                get { return keyName; }
            }

            /// <summary>
            /// The virtual key code of the key that was pressed.
            /// </summary>
            public int KeyCode
            {
                get { return keyCode; }
            }

            /// <summary>
            /// True if this key combination was passed to other applications,
            /// false if it was trapped.
            /// </summary>
            public bool PassThrough
            {
                get { return passThrough; }
            }

            public KeyboardHookEventArgs(int evtKeyCode, bool evtPassThrough)
            {
                keyName = ((Keys)evtKeyCode).ToString();
                keyCode = evtKeyCode;
                passThrough = evtPassThrough;
            }

        }

        #endregion

        #region IDisposable Members
        /// <summary>
        /// Releases the keyboard hook.
        /// </summary>
        public void Dispose()
        {
            NativeMethods.UnhookWindowsHookEx(hookID);
        }
        #endregion

        #region Native methods

        [ComVisible(false),
         System.Security.SuppressUnmanagedCodeSecurity()]
        internal class NativeMethods
        {
            [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            public static extern IntPtr GetModuleHandle(string lpModuleName);

            [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            public static extern IntPtr SetWindowsHookEx(int idHook,
                HookHandlerDelegate lpfn, IntPtr hMod, uint dwThreadId);

            [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool UnhookWindowsHookEx(IntPtr hhk);

            [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode,
                IntPtr wParam, ref KBDLLHOOKSTRUCT lParam);

            [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true, CallingConvention = CallingConvention.Winapi)]
            public static extern short GetKeyState(int keyCode);
        
        } 
 

        #endregion
    }
}


