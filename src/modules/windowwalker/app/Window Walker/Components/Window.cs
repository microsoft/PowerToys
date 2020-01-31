// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.  Code forked from Betsegaw Tadele's https://github.com/betsegaw/windowwalker/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WindowWalker.Components
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
        /// The list of icons from process so that we don't have to keep
        /// loading them from disk
        /// </summary>
        private static readonly Dictionary<uint, ImageSource> _processIdsToIconsCache = new Dictionary<uint, ImageSource>();

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
                int sizeOfTitle = InteropAndHelpers.GetWindowTextLength(hwnd);
                if (sizeOfTitle++ > 0)
                {
                    StringBuilder titleBuffer = new StringBuilder(sizeOfTitle);
                    InteropAndHelpers.GetWindowText(hwnd, titleBuffer, sizeOfTitle);
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
                        InteropAndHelpers.GetWindowThreadProcessId(Hwnd, out uint processId);
                        IntPtr processHandle = InteropAndHelpers.OpenProcess(InteropAndHelpers.ProcessAccessFlags.AllAccess, true, (int)processId);
                        StringBuilder processName = new StringBuilder(MaximumFileNameLength);

                        if (InteropAndHelpers.GetProcessImageFileName(processHandle, processName, MaximumFileNameLength) != 0)
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
                InteropAndHelpers.GetClassName(Hwnd, windowClassName, windowClassName.MaxCapacity);

                return windowClassName.ToString();
            }
        }

        /// <summary>
        /// Gets represents the Window Icon for the specified window
        /// </summary>
        public ImageSource WindowIcon
        {
            get
            {
                lock (_processIdsToIconsCache)
                {
                    InteropAndHelpers.GetWindowThreadProcessId(Hwnd, out uint processId);

                    if (!_processIdsToIconsCache.ContainsKey(processId))
                    {
                        try
                        {
                            Process process = Process.GetProcessById((int)processId);
                            Icon tempIcon = Icon.ExtractAssociatedIcon(process.Modules[0].FileName);
                            _processIdsToIconsCache.Add(processId, Imaging.CreateBitmapSourceFromHIcon(
                                tempIcon.Handle,
                                Int32Rect.Empty,
                                BitmapSizeOptions.FromEmptyOptions()));
                        }
                        catch
                        {
                            BitmapImage failedImage = new BitmapImage(new Uri(@"Images\failedIcon.jpg", UriKind.Relative));
                            _processIdsToIconsCache.Add(processId, failedImage);
                        }
                    }

                    return _processIdsToIconsCache[processId];
                }
            }
        }

        /// <summary>
        /// Gets a value indicating whether is the window visible (might return false if it is a hidden IE tab)
        /// </summary>
        public bool Visible
        {
            get
            {
                return InteropAndHelpers.IsWindowVisible(Hwnd);
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
        /// Highlights a window to help the user identify the window that has been selected
        /// </summary>
        public void HighlightWindow()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Switches dekstop focus to the window
        /// </summary>
        public void SwitchToWindow()
        {
            // The following block is necessary because
            // 1) There is a weird flashing behaviour when trying
            //    to use ShowWindow for switching tabs in IE
            // 2) SetForegroundWindow fails on minimized windows
            if (ProcessName.ToLower().Equals("iexplore.exe") || !Minimized)
            {
                InteropAndHelpers.SetForegroundWindow(Hwnd);
            }
            else
            {
                InteropAndHelpers.ShowWindow(Hwnd, InteropAndHelpers.ShowWindowCommands.Restore);
            }

            InteropAndHelpers.FlashWindow(Hwnd, true);
        }

        /// <summary>
        /// Converts the window name to string along with the process name
        /// </summary>
        /// <returns>The title of the window</returns>
        public override string ToString()
        {
            return Title + " (" + ProcessName.ToUpper() + ")";
        }

        /// <summary>
        /// Returns what the window size is
        /// </summary>
        /// <returns>The state (minimized, maximized, etc..) of the window</returns>
        public WindowSizeState GetWindowSizeState()
        {
            InteropAndHelpers.GetWindowPlacement(Hwnd, out InteropAndHelpers.WINDOWPLACEMENT placement);

            switch (placement.ShowCmd)
            {
                case InteropAndHelpers.ShowWindowCommands.Normal:
                    return WindowSizeState.Normal;
                case InteropAndHelpers.ShowWindowCommands.Minimize:
                case InteropAndHelpers.ShowWindowCommands.ShowMinimized:
                    return WindowSizeState.Minimized;
                case InteropAndHelpers.ShowWindowCommands.Maximize: // No need for ShowMaximized here since its also of value 3
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
    }
}
