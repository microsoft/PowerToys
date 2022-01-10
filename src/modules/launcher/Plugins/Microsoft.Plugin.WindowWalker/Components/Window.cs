// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// Code forked from Betsegaw Tadele's https://github.com/betsegaw/windowwalker/
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Plugin.WindowWalker.Components
{
    /// <summary>
    /// Represents a specific open window
    /// </summary>
    public class Window
    {
        /// <summary>
        /// The handle to the window
        /// </summary>
        private readonly IntPtr hwnd;

        /// <summary>
        /// The list of owners of a window so that we don't have to
        /// constantly query for the process owning a specific window
        /// </summary>
        private static readonly Dictionary<IntPtr, WindowProcess> _handlesToProcessCache = new Dictionary<IntPtr, WindowProcess>();

        /// <summary>
        /// An instance of the class WindowProcess with the important process informatrion for the window
        /// </summary>
        private readonly WindowProcess processInfo;

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

        /// <summary>
        /// Gets the object of with the process information of the window
        /// </summary>
        public WindowProcess ProcessInfo
        {
            get { return processInfo; }
        }

        /// <summary>
        /// Gets returns the name of the class for the window represented
        /// </summary>
        public string ClassName
        {
            get
            {
                return GetWindowClassName(Hwnd);
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
            processInfo = CreateWindowProcessInstance(hwnd);
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
            if (processInfo.Name.ToUpperInvariant().Equals("IEXPLORE.EXE", StringComparison.Ordinal) || !Minimized)
            {
                NativeMethods.SetForegroundWindow(Hwnd);
            }
            else
            {
                if (!NativeMethods.ShowWindow(Hwnd, NativeMethods.ShowWindowCommands.Restore))
                {
                    // ShowWindow doesn't work if the process is running elevated: fallback to SendMessage
                    _ = NativeMethods.SendMessage(Hwnd, NativeMethods.WM_SYSCOMMAND, NativeMethods.SC_RESTORE);
                }
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
            return Title + " (" + processInfo.Name.ToUpper(CultureInfo.CurrentCulture) + ")";
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
        /// Rreturns the class name of a window.
        /// </summary>
        /// <param name="hwnd">Handle to the window.</param>
        /// <returns>Class name</returns>
        private static string GetWindowClassName(IntPtr hwnd)
        {
            StringBuilder windowClassName = new StringBuilder(300);
            var numCharactersWritten = NativeMethods.GetClassName(hwnd, windowClassName, windowClassName.MaxCapacity);

            if (numCharactersWritten == 0)
            {
                return string.Empty;
            }

            return windowClassName.ToString();
        }

        /// <summary>
        /// Gets a Instance of <see cref="WindowProcess"/> form process cache or creats a new one and adds them to the cache
        /// </summary>
        /// <param name="hWindow">The handle to the window</param>
        /// <returns>A new Instance of type <see cref="WindowProcess"/></returns>
        private static WindowProcess CreateWindowProcessInstance(IntPtr hWindow)
        {
            lock (_handlesToProcessCache)
            {
                if (_handlesToProcessCache.Count > 7000)
                {
                    Debug.Print("Clearing Process Cache because it's size is " + _handlesToProcessCache.Count);
                    _handlesToProcessCache.Clear();
                }

                // Add window's process to cache if missing
                if (!_handlesToProcessCache.ContainsKey(hWindow))
                {
                    // Get process ID and name
                    WindowProcess.GetProcessIDAndThredIDFromWindowHandle(hWindow, out uint threadID, out uint processID);
                    var processName = WindowProcess.GetProcessNameFromProcessID(processID).ToString().Split('\\').Reverse().ToArray()[0];

                    if (processName.Length != 0)
                    {
                        _handlesToProcessCache.Add(hWindow, new WindowProcess(processID, threadID, processName));
                    }
                    else
                    {
                        Wox.Plugin.Logger.Log.Error($"Invalid process {processID} ({processName}) for window handle {hWindow}.", typeof(Window));
                        _handlesToProcessCache.Add(hWindow, new WindowProcess(0, 0, string.Empty));
                    }
                }

                // Correct the process data if the window belongs to a packaged app hosted by 'ApplicationFrameHost.exe'
                if (_handlesToProcessCache[hWindow].Name.ToUpperInvariant() == "APPLICATIONFRAMEHOST.EXE")
                {
                    new Task(() =>
                    {
                        NativeMethods.CallBackPtr callbackptr = new NativeMethods.CallBackPtr((IntPtr hwnd, IntPtr lParam) =>
                        {
                            if (GetWindowClassName(hwnd) == "Windows.UI.Core.CoreWindow")
                            {
                                WindowProcess.GetProcessIDAndThredIDFromWindowHandle(hwnd, out uint childThreadId, out uint childProcessId);
                                var childProcessName = WindowProcess.GetProcessNameFromProcessID(childProcessId);

                                // Update process info in cache
                                _handlesToProcessCache[hWindow].UpdateProcessInfo(childProcessId, childThreadId, childProcessName);
                                return false;
                            }
                            else
                            {
                                return true;
                            }
                        });
                        _ = NativeMethods.EnumChildWindows(hWindow, callbackptr, 0);
                    }).Start();
                }

                return _handlesToProcessCache[hWindow];
            }
        }
    }
}
