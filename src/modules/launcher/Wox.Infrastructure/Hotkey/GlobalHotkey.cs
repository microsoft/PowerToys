using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Wox.Plugin;

namespace Wox.Infrastructure.Hotkey
{
    /// <summary>
    /// Listens keyboard globally.
    /// <remarks>Uses WH_KEYBOARD_LL.</remarks>
    /// </summary>
    public class GlobalHotkey : IDisposable
    {
        private static GlobalHotkey instance;
        private InterceptKeys.LowLevelKeyboardProc hookedLowLevelKeyboardProc;
        private IntPtr hookId = IntPtr.Zero;
        public delegate bool KeyboardCallback(KeyEvent keyEvent, int vkCode, SpecialKeyState state);
        public event KeyboardCallback hookedKeyboardCallback;

        //Modifier key constants
        private const int VK_SHIFT = 0x10;
        private const int VK_CONTROL = 0x11;
        private const int VK_ALT = 0x12;
        private const int VK_WIN = 91;

        public static GlobalHotkey Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new GlobalHotkey();
                }
                return instance;
            }
        }

        private GlobalHotkey()
        {
            // We have to store the LowLevelKeyboardProc, so that it is not garbage collected runtime
            hookedLowLevelKeyboardProc = LowLevelKeyboardProc;
            // Set the hook
            hookId = InterceptKeys.SetHook(hookedLowLevelKeyboardProc);
        }

        public SpecialKeyState CheckModifiers()
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
                //WIN is pressed
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
                    if (hookedKeyboardCallback != null)
                        continues = hookedKeyboardCallback((KeyEvent)wParam.ToUInt32(), Marshal.ReadInt32(lParam), CheckModifiers());
                }
            }

            if (continues)
            {
                return InterceptKeys.CallNextHookEx(hookId, nCode, wParam, lParam);
            }
            return (IntPtr)1;
        }

        ~GlobalHotkey()
        {
            Dispose();
        }

        public void Dispose()
        {
            InterceptKeys.UnhookWindowsHookEx(hookId);
        }
    }
}