using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Input;
using static ColorPicker.Win32Apis;

namespace ColorPicker.Mouse
{
    public delegate void MouseUpEventHandler(object sender, System.Drawing.Point p);

    internal class MouseHook
    {
        private const int WH_MOUSE_LL = 14;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_MOUSEWHEEL = 0x020A;
        private const int WHEEL_DELTA = 120;

        private IntPtr _mouseHookHandle;
        private HookProc _mouseDelegate;

        private event MouseUpEventHandler MouseDown;
        public event MouseUpEventHandler OnMouseDown
        {
            add
            {
                Subscribe();
                MouseDown += value;
            }
            remove
            {
                MouseDown -= value;
                Unsubscribe();
            }
        }

        private event MouseWheelEventHandler MouseWheel;
        public event MouseWheelEventHandler OnMouseWheel
        {
            add
            {
                Subscribe();
                MouseWheel += value;
            }
            remove
            {
                MouseWheel -= value;
                Unsubscribe();
            }
        }

        private void Unsubscribe()
        {
            if (_mouseHookHandle != IntPtr.Zero)
            {
                var result = UnhookWindowsHookEx(_mouseHookHandle);
                _mouseHookHandle = IntPtr.Zero;
                _mouseDelegate = null;
                if (!result)
                {
                    int errorCode = Marshal.GetLastWin32Error();
                    throw new Win32Exception(errorCode);
                }
            }
        }

        private void Subscribe()
        {
            if (_mouseHookHandle == IntPtr.Zero)
            {
                _mouseDelegate = MouseHookProc;
                _mouseHookHandle = SetWindowsHookEx(WH_MOUSE_LL,
                    _mouseDelegate,
                    GetModuleHandle(Process.GetCurrentProcess().MainModule.ModuleName),
                    0);
                if (_mouseHookHandle == IntPtr.Zero)
                {
                    int errorCode = Marshal.GetLastWin32Error();
                    throw new Win32Exception(errorCode);
                }
            }
        }

        private IntPtr MouseHookProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                MSLLHOOKSTRUCT mouseHookStruct = (MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(MSLLHOOKSTRUCT));
                if (wParam.ToInt32() == WM_LBUTTONDOWN)
                {
                    if (MouseDown != null)
                    {
                        MouseDown.Invoke(null, new System.Drawing.Point(mouseHookStruct.pt.x, mouseHookStruct.pt.y));
                    }
                }
                if (wParam.ToInt32() == WM_MOUSEWHEEL)
                {
                    if (MouseWheel != null)
                    {
                        MouseDevice mouseDev = InputManager.Current.PrimaryMouseDevice;
                        MouseWheel.Invoke(null, new MouseWheelEventArgs(mouseDev, Environment.TickCount, (int)mouseHookStruct.mouseData >> 16));
                    }
                }
            }
            return CallNextHookEx(_mouseHookHandle, nCode, wParam, lParam);
        }
    }
}
