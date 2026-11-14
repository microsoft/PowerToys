// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using ManagedCommon;
using Microsoft.Win32.SafeHandles;

namespace KeyboardManagerEditorUI.Helpers
{
    internal static class EngineSuspendHelper
    {
        // Keep in sync with src\modules\keyboardmanager\common\KeyboardManagerConstants.h.
        private const string EditorWindowEventName = "PowerToys_KeyboardManager_Event_EditorWindow";

        private static readonly object LockObject = new();
        private static SafeWaitHandle? eventHandle;

        internal static void Acquire()
        {
            lock (LockObject)
            {
                if (eventHandle is not null)
                {
                    return;
                }

                SafeWaitHandle handle = CreateEvent(IntPtr.Zero, true, false, EditorWindowEventName);
                if (handle.IsInvalid)
                {
                    Logger.LogError($"Failed to create or open the keyboard manager editor event. Error: {Marshal.GetLastWin32Error()}");
                    handle.Dispose();
                    return;
                }

                if (!SetEvent(handle))
                {
                    Logger.LogError($"Failed to signal the keyboard manager editor event. Error: {Marshal.GetLastWin32Error()}");
                    handle.Dispose();
                    return;
                }

                eventHandle = handle;
                Logger.LogInfo("Signaled keyboard manager editor event to suspend the KBM engine");
            }
        }

        internal static void Release()
        {
            lock (LockObject)
            {
                if (eventHandle is null)
                {
                    return;
                }

                SafeWaitHandle handle = eventHandle;
                eventHandle = null;

                if (!ResetEvent(handle))
                {
                    Logger.LogError($"Failed to reset the keyboard manager editor event. Error: {Marshal.GetLastWin32Error()}");
                }

                handle.Dispose();
            }
        }

        [DllImport("kernel32.dll", EntryPoint = "CreateEventW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern SafeWaitHandle CreateEvent(IntPtr eventAttributes, bool manualReset, bool initialState, string name);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetEvent(SafeWaitHandle eventHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ResetEvent(SafeWaitHandle eventHandle);
    }
}
