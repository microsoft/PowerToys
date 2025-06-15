// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using TasklistButton = NativeMethods.TasklistButton;

namespace ShortcutGuide.Helpers
{
    internal sealed partial class TasklistPositions
    {
        public static TasklistButton[] GetButtons()
        {
            var monitor = NativeMethods.MonitorFromWindow(MainWindow.WindowHwnd, 0);
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
