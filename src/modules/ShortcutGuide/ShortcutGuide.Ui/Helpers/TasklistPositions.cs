// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using WinRT.Interop;
using TasklistButton = ShortcutGuide.NativeMethods.TasklistButton;

namespace ShortcutGuide.Helpers
{
    /// <summary>
    /// Identifies which screen edge the Windows taskbar is docked to.
    /// </summary>
    internal enum TaskbarEdge
    {
        Left,
        Top,
        Right,
        Bottom,
    }

    /// <summary>
    /// Provides methods to retrieve the positions of taskbar buttons on the current monitor.
    /// </summary>
    internal static class TasklistPositions
    {
        /// <summary>
        /// Returns the screen edge the Windows taskbar is docked to, using the
        /// public <c>SHAppBarMessage(ABM_GETTASKBARPOS)</c> shell API. The
        /// taskbar edge is a global Windows setting, so this applies to the
        /// taskbar on every monitor. Falls back to <see cref="TaskbarEdge.Bottom"/>
        /// if the query fails.
        /// </summary>
        public static TaskbarEdge GetEdge()
        {
            var data = new NativeMethods.APPBARDATA
            {
                CbSize = (uint)Marshal.SizeOf<NativeMethods.APPBARDATA>(),
            };

            if (NativeMethods.SHAppBarMessage(NativeMethods.ABM_GETTASKBARPOS, ref data) == nint.Zero)
            {
                return TaskbarEdge.Bottom;
            }

            return data.UEdge switch
            {
                NativeMethods.ABE_LEFT => TaskbarEdge.Left,
                NativeMethods.ABE_TOP => TaskbarEdge.Top,
                NativeMethods.ABE_RIGHT => TaskbarEdge.Right,
                _ => TaskbarEdge.Bottom,
            };
        }

        /// <summary>
        /// Retrieves the taskbar buttons for the current monitor.
        /// </summary>
        /// <returns>An array of the taskbar buttons.</returns>
        public static TasklistButton[] GetButtons()
        {
            var monitor = NativeMethods.MonitorFromWindow(WindowNative.GetWindowHandle(App.OverlayWindow), 0);
            nint ptr = NativeMethods.GetTasklistButtons(monitor, out int size);
            if (ptr == nint.Zero)
            {
                return [];
            }

            if (size <= 0)
            {
                return [];
            }

            TasklistButton[] buttons = new TasklistButton[size];
            nint currentPtr = ptr;
            for (int i = 0; i < size; i++)
            {
                buttons[i] = Marshal.PtrToStructure<TasklistButton>(currentPtr);
                currentPtr += Marshal.SizeOf<TasklistButton>();
            }

            return buttons;
        }
    }
}
