// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// Code forked from Betsegaw Tadele's https://github.com/betsegaw/windowwalker/
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Microsoft.Plugin.WindowWalker.Components
{
    /// <summary>
    /// Represents a specific open window
    /// </summary>
    public class Window
    {
        /// <summary>
        /// Maximum size of a file name
        /// </summary>
        private const int MaximumFileNameLength = 1000;

        /// <summary>
        /// The list of owners of a window so that we don't have to
        /// constantly query for the process owning a specific window
        /// </summary>
        private static readonly Dictionary<IntPtr, string> _handlesToProcessCache = new Dictionary<IntPtr, string>();

        /// <summary>
        /// The handle to the window
        /// </summary>
        private readonly IntPtr hwnd;

        /// <summary>
        /// Gets the title of the window (the string displayed at the top of the window)
        /// </summary>
        public string Title
        {
            get
            {
                int sizeOfTitle = NativeMethods.GetWindowTextLength(hwnd);
                if (sizeOfTitle++ > 0)
                {
                    StringBuilder titleBuffer = new StringBuilder(sizeOfTitle);
                    var numCharactersWritten = NativeMethods.GetWindowText(hwnd, titleBuffer, sizeOfTitle);
                    if (numCharactersWritten == 0)
                    {
                        return string.Empty;
                    }

                    return titleBuffer.ToString();
                }
                else
                {
                    return string.Empty;
                }
            }
        }

        /// <summary>
        /// Gets the handle to the window
        /// </summary>
        public IntPtr Hwnd
        {
            get { return hwnd; }
        }

        public uint ProcessID { get; set; }

        /// <summary>
        /// Gets returns the name of the process
        /// </summary>
        public string ProcessName
        {
            get
            {
                lock (_handlesToProcessCache)
                {
                    if (_handlesToProcessCache.Count > 7000)
                    {
                        Debug.Print("Clearing Process Cache because it's size is " + _handlesToProcessCache.Count);
                        _handlesToProcessCache.Clear();
                    }

                    if (!_handlesToProcessCache.ContainsKey(Hwnd))
                    {
                        var processName = GetProcessNameFromWindowHandle(Hwnd);

                        if (processName.Length != 0)
                        {
                            _handlesToProcessCache.Add(
                                Hwnd,
                                processName.ToString().Split('\\').Reverse().ToArray()[0]);
                        }
                        else
                        {
                            _handlesToProcessCache.Add(Hwnd, string.Empty);
                        }
                    }

                    if (_handlesToProcessCache[hwnd].ToUpperInvariant() == "APPLICATIONFRAMEHOST.EXE")
                    {
                        new Task(() =>
                        {
                            NativeMethods.CallBackPtr callbackptr = new NativeMethods.CallBackPtr((IntPtr hwnd, IntPtr lParam) =>
                            {
                                var childProcessId = GetProcessIDFromWindowHandle(hwnd);
                                if (childProcessId != ProcessID)
                                {
                                    _handlesToProcessCache[Hwnd] = GetProcessNameFromWindowHandle(hwnd);
                                    return false;
                                }
                                else
                                {
                                    return true;
                                }
                            });
                            _ = NativeMethods.EnumChildWindows(Hwnd, callbackptr, 0);
                        }).Start();
                    }

                    return _handlesToProcessCache[hwnd];
                }
            }
        }

        /// <summary>
        /// Gets returns the name of the class for the window represented
        /// </summary>
        public string ClassName
        {
            get
            {
                StringBuilder windowClassName = new StringBuilder(300);
                var numCharactersWritten = NativeMethods.GetClassName(Hwnd, windowClassName, windowClassName.MaxCapacity);

                if (numCharactersWritten == 0)
                {
                    return string.Empty;
                }

                return windowClassName.ToString();
            }
        }

        /// <summary>
        /// Gets a value indicating whether is the window visible (might return false if it is a hidden IE tab)
        /// </summary>
        public bool Visible
        {
            get
            {
                return NativeMethods.IsWindowVisible(Hwnd);
            }
        }

        /// <summary>
        /// Gets a value indicating whether determines whether the specified window handle identifies an existing window.
        /// </summary>
        public bool IsWindow
        {
            get
            {
                return NativeMethods.IsWindow(Hwnd);
            }
        }

        /// <summary>
        /// Gets a value indicating whether get a value indicating whether is the window GWL_EX_STYLE is a toolwindow
        /// </summary>
        public bool IsToolWindow
        {
            get
            {
                return (NativeMethods.GetWindowLong(Hwnd, NativeMethods.GWL_EXSTYLE) &
                    (uint)NativeMethods.ExtendedWindowStyles.WS_EX_TOOLWINDOW) ==
                    (uint)NativeMethods.ExtendedWindowStyles.WS_EX_TOOLWINDOW;
            }
        }

        /// <summary>
        /// Gets a value indicating whether get a value indicating whether the window GWL_EX_STYLE is an appwindow
        /// </summary>
        public bool IsAppWindow
        {
            get
            {
                return (NativeMethods.GetWindowLong(Hwnd, NativeMethods.GWL_EXSTYLE) &
                    (uint)NativeMethods.ExtendedWindowStyles.WS_EX_APPWINDOW) ==
                    (uint)NativeMethods.ExtendedWindowStyles.WS_EX_APPWINDOW;
            }
        }

        /// <summary>
        /// Gets a value indicating whether get a value indicating whether the window has ITaskList_Deleted property
        /// </summary>
        public bool TaskListDeleted
        {
            get
            {
                return NativeMethods.GetProp(Hwnd, "ITaskList_Deleted") != IntPtr.Zero;
            }
        }

        /// <summary>
        /// Gets a value indicating whether determines whether the specified windows is the owner
        /// </summary>
        public bool IsOwner
        {
            get
            {
                return NativeMethods.GetWindow(Hwnd, NativeMethods.GetWindowCmd.GW_OWNER) != null;
            }
        }

        /// <summary>
        /// Gets a value indicating whether returns true if the window is minimized
        /// </summary>
        public bool Minimized
        {
            get
            {
                return GetWindowSizeState() == WindowSizeState.Minimized;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Window"/> class.
        /// Initializes a new Window representation
        /// </summary>
        /// <param name="hwnd">the handle to the window we are representing</param>
        public Window(IntPtr hwnd)
        {
            // TODO: Add verification as to whether the window handle is valid
            this.hwnd = hwnd;
        }

        /// <summary>
        /// Switches desktop focus to the window
        /// </summary>
        public void SwitchToWindow()
        {
            // The following block is necessary because
            // 1) There is a weird flashing behavior when trying
            //    to use ShowWindow for switching tabs in IE
            // 2) SetForegroundWindow fails on minimized windows
            // Using Ordinal since this is internal
            if (ProcessName.ToUpperInvariant().Equals("IEXPLORE.EXE", StringComparison.Ordinal) || !Minimized)
            {
                NativeMethods.SetForegroundWindow(Hwnd);
            }
            else
            {
                NativeMethods.ShowWindow(Hwnd, NativeMethods.ShowWindowCommands.Restore);
            }

            NativeMethods.FlashWindow(Hwnd, true);
        }

        /// <summary>
        /// Converts the window name to string along with the process name
        /// </summary>
        /// <returns>The title of the window</returns>
        public override string ToString()
        {
            // Using CurrentCulture since this is user facing
            return Title + " (" + ProcessName.ToUpper(CultureInfo.CurrentCulture) + ")";
        }

        /// <summary>
        /// Returns what the window size is
        /// </summary>
        /// <returns>The state (minimized, maximized, etc..) of the window</returns>
        public WindowSizeState GetWindowSizeState()
        {
            NativeMethods.GetWindowPlacement(Hwnd, out NativeMethods.WINDOWPLACEMENT placement);

            switch (placement.ShowCmd)
            {
                case NativeMethods.ShowWindowCommands.Normal:
                    return WindowSizeState.Normal;
                case NativeMethods.ShowWindowCommands.Minimize:
                case NativeMethods.ShowWindowCommands.ShowMinimized:
                    return WindowSizeState.Minimized;
                case NativeMethods.ShowWindowCommands.Maximize: // No need for ShowMaximized here since its also of value 3
                    return WindowSizeState.Maximized;
                default:
                    // throw new Exception("Don't know how to handle window state = " + placement.ShowCmd);
                    return WindowSizeState.Unknown;
            }
        }

        /// <summary>
        /// Enum to simplify the state of the window
        /// </summary>
        public enum WindowSizeState
        {
            Normal,
            Minimized,
            Maximized,
            Unknown,
        }

        /// <summary>
        /// Gets the name of the process using the window handle
        /// </summary>
        /// <param name="hwnd">The handle to the window</param>
        /// <returns>A string representing the process name or an empty string if the function fails</returns>
        private string GetProcessNameFromWindowHandle(IntPtr hwnd)
        {
            uint processId = GetProcessIDFromWindowHandle(hwnd);
            ProcessID = processId;
            IntPtr processHandle = NativeMethods.OpenProcess(NativeMethods.ProcessAccessFlags.AllAccess, true, (int)processId);
            StringBuilder processName = new StringBuilder(MaximumFileNameLength);

            if (NativeMethods.GetProcessImageFileName(processHandle, processName, MaximumFileNameLength) != 0)
            {
                return processName.ToString().Split('\\').Reverse().ToArray()[0];
            }
            else
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Gets the process ID for the Window handle
        /// </summary>
        /// <param name="hwnd">The handle to the window</param>
        /// <returns>The process ID</returns>
        private static uint GetProcessIDFromWindowHandle(IntPtr hwnd)
        {
            _ = NativeMethods.GetWindowThreadProcessId(hwnd, out uint processId);
            return processId;
        }
    }
}
