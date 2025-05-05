// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// Code forked from Betsegaw Tadele's https://github.com/betsegaw/windowwalker/
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.WindowWalker.Helpers;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.WindowWalker.Components;

/// <summary>
/// Represents a specific open window
/// </summary>
internal sealed class Window
{
    /// <summary>
    /// The handle to the window
    /// </summary>
    private readonly IntPtr hwnd;

    /// <summary>
    /// A static cache for the process data of all known windows
    /// that we don't have to query the data every time
    /// </summary>
    private static readonly Dictionary<IntPtr, WindowProcess> _handlesToProcessCache = new();

    /// <summary>
    /// An instance of <see cref="WindowProcess"/> that contains the process information for the window
    /// </summary>
    private readonly WindowProcess processInfo;

    /// <summary>
    /// An instance of <see cref="VDesktop"/> that contains the desktop information for the window
    /// </summary>
    private readonly VDesktop desktopInfo;

    /// <summary>
    /// Gets the title of the window (the string displayed at the top of the window)
    /// </summary>
    internal string Title
    {
        get
        {
            var sizeOfTitle = NativeMethods.GetWindowTextLength(hwnd);
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
    internal IntPtr Hwnd => hwnd;

    /// <summary>
    /// Gets the object of with the process information of the window
    /// </summary>
    internal WindowProcess Process => processInfo;

    /// <summary>
    /// Gets the object of with the desktop information of the window
    /// </summary>
    internal VDesktop Desktop => desktopInfo;

    /// <summary>
    /// Gets the name of the class for the window represented
    /// </summary>
    internal string ClassName => GetWindowClassName(Hwnd);

    /// <summary>
    /// Gets a value indicating whether the window is visible (might return false if it is a hidden IE tab)
    /// </summary>
    internal bool Visible => NativeMethods.IsWindowVisible(Hwnd);

    /// <summary>
    /// Gets a value indicating whether the window is cloaked (true) or not (false).
    /// (A cloaked window is not visible to the user. But the window is still composed by DWM.)
    /// </summary>
    internal bool IsCloaked => GetWindowCloakState() != WindowCloakState.None;

    /// <summary>
    /// Gets a value indicating whether the specified window handle identifies an existing window.
    /// </summary>
    internal bool IsWindow => NativeMethods.IsWindow(Hwnd);

    /// <summary>
    /// Gets a value indicating whether the window is a toolwindow
    /// </summary>
    internal bool IsToolWindow => (NativeMethods.GetWindowLong(Hwnd, Win32Constants.GWL_EXSTYLE) &
                (uint)ExtendedWindowStyles.WS_EX_TOOLWINDOW) ==
                (uint)ExtendedWindowStyles.WS_EX_TOOLWINDOW;

    /// <summary>
    /// Gets a value indicating whether the window is an appwindow
    /// </summary>
    internal bool IsAppWindow => (NativeMethods.GetWindowLong(Hwnd, Win32Constants.GWL_EXSTYLE) &
                (uint)ExtendedWindowStyles.WS_EX_APPWINDOW) ==
                (uint)ExtendedWindowStyles.WS_EX_APPWINDOW;

    /// <summary>
    /// Gets a value indicating whether the window has ITaskList_Deleted property
    /// </summary>
    internal bool TaskListDeleted => NativeMethods.GetProp(Hwnd, "ITaskList_Deleted") != IntPtr.Zero;

    /// <summary>
    /// Gets a value indicating whether the specified windows is the owner (i.e. doesn't have an owner)
    /// </summary>
    internal bool IsOwner => NativeMethods.GetWindow(Hwnd, GetWindowCmd.GW_OWNER) == IntPtr.Zero;

    /// <summary>
    /// Gets a value indicating whether the window is minimized
    /// </summary>
    internal bool Minimized => GetWindowSizeState() == WindowSizeState.Minimized;

    /// <summary>
    /// Initializes a new instance of the <see cref="Window"/> class.
    /// Initializes a new Window representation
    /// </summary>
    /// <param name="hwnd">the handle to the window we are representing</param>
    internal Window(IntPtr hwnd)
    {
        // TODO: Add verification as to whether the window handle is valid
        this.hwnd = hwnd;
        processInfo = CreateWindowProcessInstance(hwnd);
        desktopInfo = WindowWalkerCommandsProvider.VirtualDesktopHelperInstance.GetWindowDesktop(hwnd);
    }

    /// <summary>
    /// Switches desktop focus to the window
    /// </summary>
    internal void SwitchToWindow()
    {
        // The following block is necessary because
        // 1) There is a weird flashing behavior when trying
        //    to use ShowWindow for switching tabs in IE
        // 2) SetForegroundWindow fails on minimized windows
        // Using Ordinal since this is internal
        if (processInfo.Name?.ToUpperInvariant().Equals("IEXPLORE.EXE", StringComparison.Ordinal) == true || !Minimized)
        {
            NativeMethods.SetForegroundWindow(Hwnd);
        }
        else
        {
            if (!NativeMethods.ShowWindow(Hwnd, ShowWindowCommand.Restore))
            {
                // ShowWindow doesn't work if the process is running elevated: fallback to SendMessage
                _ = NativeMethods.SendMessage(Hwnd, Win32Constants.WM_SYSCOMMAND, Win32Constants.SC_RESTORE);
            }
        }

        NativeMethods.FlashWindow(Hwnd, true);
    }

    /// <summary>
    /// Helper function to close the window
    /// </summary>
    internal void CloseThisWindowHelper()
    {
        _ = NativeMethods.SendMessageTimeout(Hwnd, Win32Constants.WM_SYSCOMMAND, Win32Constants.SC_CLOSE, 0, 0x0000, 5000, out _);
    }

    /// <summary>
    /// Closes the window
    /// </summary>
    internal void CloseThisWindow()
    {
        Thread thread = new(new ThreadStart(CloseThisWindowHelper));
        thread.Start();
    }

    /// <summary>
    /// Converts the window name to string along with the process name
    /// </summary>
    /// <returns>The title of the window</returns>
    public override string ToString()
    {
        // Using CurrentCulture since this is user facing
        return Title + " (" + processInfo.Name?.ToUpper(CultureInfo.CurrentCulture) + ")";
    }

    /// <summary>
    /// Returns what the window size is
    /// </summary>
    /// <returns>The state (minimized, maximized, etc..) of the window</returns>
    internal WindowSizeState GetWindowSizeState()
    {
        NativeMethods.GetWindowPlacement(Hwnd, out WINDOWPLACEMENT placement);

        switch (placement.ShowCmd)
        {
            case ShowWindowCommand.Normal:
                return WindowSizeState.Normal;
            case ShowWindowCommand.Minimize:
            case ShowWindowCommand.ShowMinimized:
                return WindowSizeState.Minimized;
            case ShowWindowCommand.Maximize: // No need for ShowMaximized here since its also of value 3
                return WindowSizeState.Maximized;
            default:
                // throw new Exception("Don't know how to handle window state = " + placement.ShowCmd);
                return WindowSizeState.Unknown;
        }
    }

    /// <summary>
    /// Enum to simplify the state of the window
    /// </summary>
    internal enum WindowSizeState
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
    internal WindowCloakState GetWindowCloakState()
    {
        _ = NativeMethods.DwmGetWindowAttribute(Hwnd, (int)DwmWindowAttributes.Cloaked, out var isCloakedState, sizeof(uint));

        switch (isCloakedState)
        {
            case (int)DwmWindowCloakStates.None:
                return WindowCloakState.None;
            case (int)DwmWindowCloakStates.CloakedApp:
                return WindowCloakState.App;
            case (int)DwmWindowCloakStates.CloakedShell:
                return WindowWalkerCommandsProvider.VirtualDesktopHelperInstance.IsWindowCloakedByVirtualDesktopManager(hwnd, Desktop.Id) ? WindowCloakState.OtherDesktop : WindowCloakState.Shell;
            case (int)DwmWindowCloakStates.CloakedInherited:
                return WindowCloakState.Inherited;
            default:
                return WindowCloakState.Unknown;
        }
    }

    /// <summary>
    /// Enum to simplify the cloak state of the window
    /// </summary>
    internal enum WindowCloakState
    {
        None,
        App,
        Shell,
        Inherited,
        OtherDesktop,
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
                    // For the dwm process we cannot receive the name. This is no problem because the window isn't part of result list.
                    ExtensionHost.LogMessage(new LogMessage() { Message = $"Invalid process {processId} ({processName}) for window handle {hWindow}." });
                    _handlesToProcessCache.Add(hWindow, new WindowProcess(0, 0, string.Empty));
                }
            }

            // Correct the process data if the window belongs to a uwp app hosted by 'ApplicationFrameHost.exe'
            // (This only works if the window isn't minimized. For minimized windows the required child window isn't assigned.)
            if (string.Equals(_handlesToProcessCache[hWindow].Name, "ApplicationFrameHost.exe", StringComparison.OrdinalIgnoreCase))
            {
                new Task(() =>
                {
                    EnumWindowsProc callbackptr = new EnumWindowsProc((IntPtr hwnd, IntPtr lParam) =>
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
