using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ColorPicker.ColorPickingFunctionality.SystemEvents
{
    abstract class SystemHook
    {
        public delegate int HookProcDelegate(int nCode, int wParam, IntPtr lParam);

        private int hookType;
        private int hookHandleID;
        private HookProcDelegate hookActionDelegate;

        /// <summary> 
        /// This method registers a windows event hook. 
        /// </summary>
        /// <param name="hookID"> The type of hook procedure to be installed. </param>
        /// <param name="lpfn"> a delegate which will be called when the hook is triggered. </param>
        /// <param name="hMod"> A handle to the DLL containing the hook procedure pointed to by the lpfn parameter. </param>
        /// <param name="dwThreadID"> ID of thread which even occurs on, set to 0 for all threads. </param>
        /// <returns> The ID of the hook handle, or 0 on error. </returns>
        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowsHookEx(int idHook, HookProcDelegate lpfn, IntPtr hMod, int dwThreadId);

        /// <summary> 
        /// This method removes a previously registered event hook. 
        /// </summary>
        /// <param name="hookHandle"> This is the handle of the hook to be removed. </param>
        /// <returns> 1 on success, 0 on failure. </returns>
        [DllImport("user32.dll", SetLastError = true)]
        private static extern int UnhookWindowsHookEx(int hookHandle);

        /// <summary> 
        /// This method calls the next hook in the chain. This allows the event to pass through if you do not want to sotp it. 
        /// </summary>
        /// <param name="hookHandle"> This is the handle of the hook to be removed. </param>
        /// <param name="nCode"> The hook code passed to the current hook procedure. </param>
        /// <param name="wParam"> Specifies whether the message is sent by the current process (Non-zero if from current process, NULL otherwise). </param>
        /// <param name="lParam"> A pointer to a structure that contains details about the message. </param>
        /// <returns> The value returned by the next hook in the chain. The current hook also needs to return this vlaue. </returns>
        [DllImport("user32.dll")]
        private static extern int CallNextHookEx(int hookHandle, int nCode, int wParam, IntPtr lParam);

        /// <summary>
        /// Retrieves a module handle for the specified module.
        /// </summary>
        /// <param name="moduleName"> The name of the loaded module (either a .dll or .exe file). </param>
        /// <returns> A handle to specified file, or NULL on failure. </returns>
        [DllImport("kernel32.dll")]
        private static extern IntPtr GetModuleHandle(string moduleName);

        public SystemHook(int hookType)
        {
            hookActionDelegate += HookProc;
            this.hookType = hookType;
            ActivateHook();
        }

        ~SystemHook()
        {
            DeactivateHook();
        }

        public abstract int HookProc(int nCode, int wParam, IntPtr lParam);

        public int CallNextHookExWrapper(int nCode, int wParam, IntPtr lParam)
        {
            return CallNextHookEx(hookHandleID, nCode, wParam, lParam);
        }

        public void ActivateHook()
        {
            if (hookHandleID == 0)
            {
                hookHandleID = SetWindowsHookEx(
                    hookType,
                    hookActionDelegate,
                    SafeGetModuleHandle(Process.GetCurrentProcess().MainModule.ModuleName),
                    0);
                if (hookHandleID == 0)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }
            }
        }

        private IntPtr SafeGetModuleHandle(string moduleName)
        {
            IntPtr handle = GetModuleHandle(moduleName);
            if (handle == IntPtr.Zero)
            {
                throw new InternalSystemCallException("Failed to get module handle");
            }
            return handle;
        }

        public void DeactivateHook()
        {
            if (hookHandleID != 0)
            {
                int result = UnhookWindowsHookEx(hookHandleID);
                hookHandleID = 0;
                if (result == 0)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }
            }
        }
    }
}
