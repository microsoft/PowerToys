using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Threading;

namespace WinAlfred
{
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

    public class SpecialKeyState
    {
        public bool CtrlPressed { get; set; }
        public bool ShiftPressed { get; set; }
        public bool AltPressed { get; set; }
        public bool WinPressed { get; set; }
    }

    /// <summary>
    /// Listens keyboard globally.
    /// 
    /// <remarks>Uses WH_KEYBOARD_LL.</remarks>
    /// </summary>
    public class KeyboardListener : IDisposable
    {
        private InterceptKeys.LowLevelKeyboardProc hookedLowLevelKeyboardProc;
        private IntPtr hookId = IntPtr.Zero;
        public delegate bool KeyboardCallback(KeyEvent keyEvent, int vkCode, SpecialKeyState state);
        public event KeyboardCallback hookedKeyboardCallback;

        //Modifier key constants
        private const int VK_SHIFT = 0x10;
        private const int VK_CONTROL = 0x11;
        private const int VK_ALT = 0x12;
        private const int VK_WIN = 91;

        public KeyboardListener()
        {
            // We have to store the LowLevelKeyboardProc, so that it is not garbage collected runtime
            hookedLowLevelKeyboardProc = LowLevelKeyboardProc;
            // Set the hook
            hookId = InterceptKeys.SetHook(hookedLowLevelKeyboardProc);
        }

        private SpecialKeyState CheckModifiers()
        {
            SpecialKeyState state = new SpecialKeyState();
            if ((InterceptKeys.GetKeyState(VK_SHIFT) & 0x8000) != 0)
            {
                //SHIFT is pressed
                state.ShiftPressed = true;
            }
            if ((InterceptKeys.GetKeyState(VK_CONTROL) & 0x8000) != 0)
            {
                //CONTROL is pressed
                state.CtrlPressed = true;
            }
            if ((InterceptKeys.GetKeyState(VK_ALT) & 0x8000) != 0)
            {
                //ALT is pressed
                state.AltPressed = true;
            }
            if ((InterceptKeys.GetKeyState(VK_WIN) & 0x8000) != 0)
            {
                //ALT is pressed
                state.WinPressed = true;
            }

            return state;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private IntPtr LowLevelKeyboardProc(int nCode, UIntPtr wParam, IntPtr lParam)
        {
            bool continues = true;

            if (nCode >= 0)
            {
                if (wParam.ToUInt32() == (int)KeyEvent.WM_KEYDOWN ||
                    wParam.ToUInt32() == (int)KeyEvent.WM_KEYUP ||
                    wParam.ToUInt32() == (int)KeyEvent.WM_SYSKEYDOWN ||
                    wParam.ToUInt32() == (int)KeyEvent.WM_SYSKEYUP)
                {
                    continues = hookedKeyboardCallback((KeyEvent)wParam.ToUInt32(), Marshal.ReadInt32(lParam), CheckModifiers());
                }
            }

            if (continues)
            {
                return InterceptKeys.CallNextHookEx(hookId, nCode, wParam, lParam);
            }
            return (IntPtr)1;
        }

        ~KeyboardListener()
        {
            Dispose();
        }

        public void Dispose()
        {
            InterceptKeys.UnhookWindowsHookEx(hookId);
        }
    }

    public static class InterceptKeys
    {
        public delegate IntPtr LowLevelKeyboardProc(int nCode, UIntPtr wParam, IntPtr lParam);

        private static int WH_KEYBOARD_LL = 13;

        public static IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, UIntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true, CallingConvention = CallingConvention.Winapi)]
        public static extern short GetKeyState(int keyCode);

        [DllImport("user32.dll")]
        internal static extern uint SendInput(uint nInputs, [MarshalAs(UnmanagedType.LPArray), In] INPUT[] pInputs, int cbSize);


        public static void SendKeyStroke(int funckey, int key)
        {
            INPUT[] input = new INPUT[4];
            input[0].type = input[1].type = input[2].type = input[3].type = (int)InputType.INPUT_KEYBOARD;
            input[0].ki.wVk = input[2].ki.wVk = (short) funckey;
            input[1].ki.wVk = input[3].ki.wVk = (short) key;

            input[2].ki.dwFlags = input[3].ki.dwFlags =(int) KEYEVENTF.KEYUP;

            SendInput((uint)input.Length, input, Marshal.SizeOf(input[0]));
        }
    }

    public enum InputType
    {
        INPUT_MOUSE = 0,
        INPUT_KEYBOARD = 1,
        INPUT_HARDWARE = 2,
    }
    [Flags()]
    public enum MOUSEEVENTF
    {
        MOVE = 0x0001,  //mouse move   
        LEFTDOWN = 0x0002,  //left button down   
        LEFTUP = 0x0004,  //left button up   
        RIGHTDOWN = 0x0008,  //right button down   
        RIGHTUP = 0x0010,  //right button up   
        MIDDLEDOWN = 0x0020, //middle button down   
        MIDDLEUP = 0x0040,  //middle button up   
        XDOWN = 0x0080,  //x button down   
        XUP = 0x0100,  //x button down   
        WHEEL = 0x0800,  //wheel button rolled   
        VIRTUALDESK = 0x4000,  //map to entire virtual desktop   
        ABSOLUTE = 0x8000,  //absolute move   
    }

    [Flags()]
    public enum KEYEVENTF
    {
        EXTENDEDKEY = 0x0001,
        KEYUP = 0x0002,
        UNICODE = 0x0004,
        SCANCODE = 0x0008,
    }
    [StructLayout(LayoutKind.Explicit)]
    public struct INPUT
    {
        [FieldOffset(0)]
        public Int32 type;//0-MOUSEINPUT;1-KEYBDINPUT;2-HARDWAREINPUT   
        [FieldOffset(4)]
        public KEYBDINPUT ki;
        [FieldOffset(4)]
        public MOUSEINPUT mi;
        [FieldOffset(4)]
        public HARDWAREINPUT hi;
    }
    [StructLayout(LayoutKind.Sequential)]
    public struct MOUSEINPUT
    {
        public Int32 dx;
        public Int32 dy;
        public Int32 mouseData;
        public Int32 dwFlags;
        public Int32 time;
        public IntPtr dwExtraInfo;
    }
    [StructLayout(LayoutKind.Sequential)]
    public struct KEYBDINPUT
    {
        public Int16 wVk;
        public Int16 wScan;
        public Int32 dwFlags;
        public Int32 time;
        public IntPtr dwExtraInfo;
    }
    [StructLayout(LayoutKind.Sequential)]
    public struct HARDWAREINPUT
    {
        public Int32 uMsg;
        public Int16 wParamL;
        public Int16 wParamH;
    }
}