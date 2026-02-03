// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using WinRT.Interop;
using TasklistButton = ShortcutGuide.NativeMethods.TasklistButton;

namespace ShortcutGuide.Helpers
{
    /// <summary>
    /// Provides methods to retrieve the positions of taskbar buttons on the current monitor.
    /// </summary>
    internal static class TasklistPositions
    {
        /// <summary>
        /// Retrieves the taskbar buttons for the current monitor.
        /// </summary>
        /// <returns>An array of the taskbar buttons.</returns>
        public static TasklistButton[] GetButtons()
        {
            var monitor = NativeMethods.MonitorFromWindow(WindowNative.GetWindowHandle(App.MainWindow), 0);
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
