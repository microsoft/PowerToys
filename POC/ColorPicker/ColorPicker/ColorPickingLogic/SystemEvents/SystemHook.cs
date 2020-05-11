using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace UI
{
    abstract class SystemHook
    {
        public int hookType;
        public static int eventHookHandle;
        public delegate int HookProcDelegate(int nCode, int wParam, IntPtr lParam);
        public HookProcDelegate eventActionDelegate;

        [DllImport("user32.dll", SetLastError = true)]
        public static extern int SetWindowsHookEx(int idHook, HookProcDelegate lpfn, IntPtr hMod, int dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern int UnhookWindowsHookEx(int idHook);

        [DllImport("user32.dll")]
        public static extern int CallNextHookEx(int idHook, int nCode, int wParam, IntPtr lParam);

        [DllImport("kernel32.dll")]
        public static extern IntPtr GetModuleHandle(string name);

        public SystemHook(int hookType)
        {
            this.hookType = hookType;
            eventActionDelegate += HookProc;
            CaptureGlobalEvent();
        }

        ~SystemHook()
        {
            ReleaseGlobalEvent();
        }

        public abstract int HookProc(int nCode, int wParam, IntPtr lParam);

        public void CaptureGlobalEvent()
        {
            if (eventHookHandle == 0)
            {
                eventHookHandle = SetWindowsHookEx(
                    hookType, // event which trigger the hook
                    eventActionDelegate, // hook procedure called when the event fires
                    GetModuleHandle(Process.GetCurrentProcess().MainModule.ModuleName), // A handle to the DLL containing the hook procedure pointed to by the lpfn parameter
                    0); // associate hook with all running threads
                if (eventHookHandle == 0)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }
            }
        }

        private void ReleaseGlobalEvent()
        {
            if (eventHookHandle != 0)
            {
                int result = UnhookWindowsHookEx(eventHookHandle);
                if (result == 0)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }
            }
        }
    }
}
