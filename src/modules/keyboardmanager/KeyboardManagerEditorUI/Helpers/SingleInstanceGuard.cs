// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using ManagedCommon;

namespace KeyboardManagerEditorUI.Helpers
{
    /// <summary>
    /// Keeps a single editor instance alive at a time.
    /// </summary>
    /// <remarks>
    /// Two instances would race on the same <c>default.json</c> and <c>editorSettings.json</c>, with
    /// the last writer winning. Both launchers de-duplicate already, but against their own process
    /// handle only - Settings tracks the process it started and the module tracks
    /// <c>m_hEditorProcess</c> - so opening the editor from Settings and then through the hotkey
    /// starts a second one. The classic editor guards this with the same named mutex.
    /// </remarks>
    internal static class SingleInstanceGuard
    {
        /// <summary>
        /// Same name the classic editor uses (<c>KeyboardManagerEditor.cpp</c>). Sharing it also
        /// keeps the two editors from running side by side if <c>useNewEditor</c> is toggled while
        /// one of them is open - they write the same configuration.
        /// </summary>
        private const string MutexName = @"Local\PowerToys_KBMEditor_InstanceMutex";

        private const string EditorProcessName = "PowerToys.KeyboardManagerEditorUI";

        /// <summary>Win32 <c>SW_RESTORE</c>.</summary>
        private const int SwRestore = 9;

        // Held for the lifetime of the process; closing the handle on exit releases it.
        private static Mutex? _mutex;

        /// <summary>
        /// Returns false when another editor instance already holds the mutex.
        /// </summary>
        public static bool TryAcquire()
        {
            try
            {
                var mutex = new Mutex(true, MutexName, out bool createdNew);
                if (!createdNew)
                {
                    mutex.Dispose();
                    return false;
                }

                _mutex = mutex;
                return true;
            }
            catch (Exception ex)
            {
                // Never block startup on the guard itself.
                Logger.LogWarning($"Could not create the single-instance mutex, starting anyway: {ex.Message}");
                return true;
            }
        }

        /// <summary>
        /// Brings the editor window of the instance that is already running to the foreground, so
        /// pressing the hotkey a second time does something visible instead of nothing.
        /// </summary>
        public static void ActivateExistingInstance()
        {
            try
            {
                int currentProcessId = Environment.ProcessId;
                foreach (Process process in Process.GetProcessesByName(EditorProcessName))
                {
                    using (process)
                    {
                        if (process.Id == currentProcessId)
                        {
                            continue;
                        }

                        IntPtr hwnd = process.MainWindowHandle;
                        if (hwnd == IntPtr.Zero)
                        {
                            continue;
                        }

                        if (IsIconic(hwnd))
                        {
                            ShowWindow(hwnd, SwRestore);
                        }

                        SetForegroundWindow(hwnd);
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Could not bring the running editor to the foreground: {ex.Message}");
            }
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    }
}
