// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// Code forked from Betsegaw Tadele's https://github.com/betsegaw/windowwalker/
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using Wox.Plugin.Logger;

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
        /// A static cache for the process data of all known windows
        /// that we don't have to query the data every time
        /// </summary>
        private static readonly Dictionary<IntPtr, WindowProcess> _handlesToProcessCache = new Dictionary<IntPtr, WindowProcess>();

        /// <summary>
        /// An instance of <see cref="WindowProcess"/> that contains the process information for the window
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
        /// Gets the name of the class for the window represented
        /// </summary>
        public string ClassName
        {
            get
            {
                return GetWindowClassName(Hwnd);
            }
        }

        /// <summary>
        /// Gets a value indicating whether the window is visible (might return false if it is a hidden IE tab)
        /// </summary>
        public bool Visible
        {
            get
            {
                return NativeMethods.IsWindowVisible(Hwnd);
            }
        }

        /// <summary>
        /// Gets a value indicating whether the window is cloaked (true) or not (false).
        /// (A cloaked window is not visible to the user. But the window is still composed by DWM.)
        /// </summary>
        public bool IsCloaked
        {
            get
            {
                return GetWindowCloakState() != WindowCloakState.None;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the specified window handle identifies an existing window.
        /// </summary>
        public bool IsWindow
        {
            get
            {
                return NativeMethods.IsWindow(Hwnd);
            }
        }

        /// <summary>
        /// Gets a value indicating whether the window is a toolwindow
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
        /// Gets a value indicating whether the window is an appwindow
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
        /// Gets a value indicating whether the window has ITaskList_Deleted property
        /// </summary>
        public bool TaskListDeleted
        {
            get
            {
                return NativeMethods.GetProp(Hwnd, "ITaskList_Deleted") != IntPtr.Zero;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the specified windows is the owner (i.e. doesn't have an owner)
        /// </summary>
        public bool IsOwner
        {
            get
            {
                return NativeMethods.GetWindow(Hwnd, NativeMethods.GetWindowCmd.GW_OWNER) == IntPtr.Zero;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the window is minimized
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
        /// Returns the window cloak state from DWM
        /// (A cloaked window is not visible to the user. But the window is still composed by DWM.)
        /// </summary>
        /// <returns>The state (none, app, ...) of the window</returns>
        public WindowCloakState GetWindowCloakState()
        {
            _ = NativeMethods.DwmGetWindowAttribute(Hwnd, (int)NativeMethods.DwmWindowAttribute.Cloaked, out int isCloakedState, sizeof(uint));

            switch (isCloakedState)
            {
                case (int)NativeMethods.DwmWindowCloakState.None:
                    return WindowCloakState.None;
                case (int)NativeMethods.DwmWindowCloakState.CloakedApp:
                    return WindowCloakState.App;
                case (int)NativeMethods.DwmWindowCloakState.CloakedShell:
                    return WindowCloakState.Shell;
                case (int)NativeMethods.DwmWindowCloakState.CloakedInherited:
                    return WindowCloakState.Inherited;
                default:
                    return WindowCloakState.Unknown;
            }
        }

        /// <summary>
        /// Enum to simplify the cloak state of the window
        /// </summary>
        public enum WindowCloakState
        {
            None,
            App,
            Shell,
            Inherited,
            Unknown,
        }

        /// <summary>
        /// Returns the class name of a window.
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
        /// Gets an instance of <see cref="WindowProcess"/> form process cache or creates a new one. A new one will be added to the cache.
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
                    var processId = WindowProcess.GetProcessIDFromWindowHandle(hWindow);
                    var threadId = WindowProcess.GetThreadIDFromWindowHandle(hWindow);
                    var processName = WindowProcess.GetProcessNameFromProcessID(processId);

                    if (processName.Length != 0)
                    {
                        _handlesToProcessCache.Add(hWindow, new WindowProcess(processId, threadId, processName));
                    }
                    else
                    {
                        // For the dwm process we can not receive the name. This is no problem because the window isn't part of result list.
                        Log.Debug($"Invalid process {processId} ({processName}) for window handle {hWindow}.", typeof(Window));
                        _handlesToProcessCache.Add(hWindow, new WindowProcess(0, 0, string.Empty));
                    }
                }

                // Correct the process data if the window belongs to a uwp app hosted by 'ApplicationFrameHost.exe'
                // (This only works if the window isn't minimized. For minimized windows the required child window isn't assigned.)
                if (_handlesToProcessCache[hWindow].Name.ToUpperInvariant() == "APPLICATIONFRAMEHOST.EXE")
                {
                    new Task(() =>
                    {
                        NativeMethods.CallBackPtr callbackptr = new NativeMethods.CallBackPtr((IntPtr hwnd, IntPtr lParam) =>
                        {
                            // Every uwp app main window has at least three child windows. Only the one we are interested in has a class starting with "Windows.UI.Core." and is assigned to the real app process.
                            // (The other ones have a class name that begins with the string "ApplicationFrame".)
                            if (GetWindowClassName(hwnd).StartsWith("Windows.UI.Core.", StringComparison.OrdinalIgnoreCase))
                            {
                                var childProcessId = WindowProcess.GetProcessIDFromWindowHandle(hwnd);
                                var childThreadId = WindowProcess.GetThreadIDFromWindowHandle(hwnd);
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
