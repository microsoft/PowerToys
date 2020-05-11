using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ColorPicker.ColorPickingFunctionality.SystemEvents
{
    abstract class SystemHook
    {
        private int hookType;
        private int hookHandleID;
        private HookProcDelegate hookActionDelegate;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowsHookEx(int idHook, HookProcDelegate lpfn, IntPtr hMod, int dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int UnhookWindowsHookEx(int idHook);

        [DllImport("user32.dll")]
        private static extern int CallNextHookEx(int idHook, int nCode, int wParam, IntPtr lParam);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetModuleHandle(string name);

        public delegate int HookProcDelegate(int nCode, int wParam, IntPtr lParam);

        public SystemHook(int hookType)
        {
            hookActionDelegate += HookProc;
            this.hookType = hookType;
            CaptureGlobalEvent();
        }

        ~SystemHook()
        {
            ReleaseGlobalEvent();
        }

        public abstract int HookProc(int nCode, int wParam, IntPtr lParam);

        public int CallNextHookExWrapper(int nCode, int wParam, IntPtr lParam)
        {
            return CallNextHookEx(hookHandleID, nCode, wParam, lParam);
        }

        private void CaptureGlobalEvent()
        {
            if (hookHandleID == 0)
            {
                hookHandleID = SetWindowsHookEx(
                    hookType, // event which trigger the hook
                    hookActionDelegate, // hook procedure called when the event fires
                    GetModuleHandle(Process.GetCurrentProcess().MainModule.ModuleName), // A handle to the DLL containing the hook procedure pointed to by the lpfn parameter
                    0); // associate hook with all running threads
                if (hookHandleID == 0)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }
            }
        }

        private void ReleaseGlobalEvent()
        {
            if (hookHandleID != 0)
            {
                int result = UnhookWindowsHookEx(hookHandleID);
                if (result == 0)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }
            }
        }
    }
}
