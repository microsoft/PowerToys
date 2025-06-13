// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UIAutomationClient;

namespace ShortcutGuide
{
    internal sealed partial class TasklistPositions
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct TasklistButton
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string Name;

            public int X;

            public int Y;

            public int Width;

            public int Height;

            public int Keynum;
        }

        /*
        private IUIAutomation? _automation;
        private IUIAutomationElement? _element;
        private IUIAutomationCondition? _trueCondition;
                public void Update()
                {

                    // Get HWND of the tasklist
                    var tasklistHwnd = NativeMethods.FindWindowA("Shell_TrayWnd", null);
                    if (tasklistHwnd == IntPtr.Zero)
                    {
                        return;
                    }

                    tasklistHwnd = NativeMethods.FindWindowExA(tasklistHwnd, IntPtr.Zero, "ReBarWindow32", string.Empty);
                    if (tasklistHwnd == IntPtr.Zero)
                    {
                        return;
                    }

                    tasklistHwnd = NativeMethods.FindWindowExA(tasklistHwnd, IntPtr.Zero, "MSTaskSwWClass", string.Empty);
                    if (tasklistHwnd == IntPtr.Zero)
                    {
                        return;
                    }

                    tasklistHwnd = NativeMethods.FindWindowExA(tasklistHwnd, IntPtr.Zero, "MSTaskListWClass", string.Empty);
                    if (tasklistHwnd == IntPtr.Zero)
                    {
                        return;
                    }

                    if (_automation == null)
                    {
                        _automation = new CUIAutomation();
                        _trueCondition = _automation.CreateTrueCondition();
                    }

                    _element = null;
                    _element = _automation.ElementFromHandle(tasklistHwnd);
                }

                public bool UpdateButtons(List<TasklistButton> buttons)
                {
                    if (_automation == null || _element == null)
                    {
                        return false;
                    }

                    IUIAutomationElementArray elements = _element.FindAll(TreeScope.TreeScope_Children, _trueCondition);
                    if (elements == null)
                    {
                        return false;
                    }

                    int count = elements.Length;
                    var foundButtons = new List<TasklistButton>(count);

                    for (int i = 0; i < count; ++i)
                    {
                        var child = elements.GetElement(i);
                        var button = default(TasklistButton);

                        object rectObj = child.GetCurrentPropertyValue(30001);
                        if (rectObj is double[] arr && arr.Length == 4)
                        {
                            button.X = (long)arr[0];
                            button.Y = (long)arr[1];
                            button.Width = (long)arr[2];
                            button.Height = (long)arr[3];
                        }
                        else if (rectObj is Windows.Foundation.Rect wrect)
                        {
                            button.X = (long)wrect.X;
                            button.Y = (long)wrect.Y;
                            button.Width = (long)wrect.Width;
                            button.Height = (long)wrect.Height;
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"rectObj type: {rectObj?.GetType()} value: {rectObj}");
                            continue; // Don't return false, just skip
                        }

                        object nameObj = child.GetCurrentPropertyValue(30011);
                        button.Name = nameObj as string ?? string.Empty;

                        foundButtons.Add(button);
                    }

                    // assign keynums
                    buttons.Clear();
                    foreach (var button in foundButtons)
                    {
                        if (buttons.Count == 0)
                        {
                            var b = button;
                            b.KeyNumber = 1;
                            buttons.Add(b);
                        }
                        else
                        {
                            var last = buttons[^1];
                            if (button.X < last.X || button.Y < last.Y)
                            {
                                break;
                            }

                            if (button.Name == last.Name)
                            {
                                continue;
                            }

                            var b = button;
                            b.KeyNumber = last.KeyNumber + 1;
                            buttons.Add(b);
                            if (b.KeyNumber == 10)
                            {
                                break;
                            }
                        }
                    }

                    return true;
                }*/

        [DllImport("ShortcutGuide.CPPProject.dll", EntryPoint = "get_buttons")]
        public static extern IntPtr GetTasklistButtons(nint monitor, out int size);

        [LibraryImport("User32.dll")]
        private static partial IntPtr MonitorFromWindow(nint hwnd, int dwFlags);

        public static TasklistButton[] GetButtons()
        {
            var monitor = MonitorFromWindow(MainWindow.WindowHwnd, 0);
            IntPtr ptr = GetTasklistButtons(monitor, out int size);
            if (ptr == IntPtr.Zero)
            {
                return [];
            }

            if (size <= 0)
            {
                return [];
            }

            TasklistButton[] buttons = new TasklistButton[size];
            IntPtr currentPtr = ptr;
            for (int i = 0; i < size; i++)
            {
                buttons[i] = Marshal.PtrToStructure<TasklistButton>(currentPtr);
                currentPtr += Marshal.SizeOf<TasklistButton>();
            }

            return buttons;
        }
    }
}
