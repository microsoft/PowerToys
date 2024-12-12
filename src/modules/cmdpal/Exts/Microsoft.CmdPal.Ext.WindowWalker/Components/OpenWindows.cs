// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// Code forked from Betsegaw Tadele's https://github.com/betsegaw/windowwalker/
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.CmdPal.Ext.WindowWalker.Helpers;

namespace Microsoft.CmdPal.Ext.WindowWalker.Components;

/// <summary>
/// Class that represents the state of the desktops windows
/// </summary>
internal sealed class OpenWindows
{
    /// <summary>
    /// Used to enforce single execution of EnumWindows
    /// </summary>
    private static readonly object _enumWindowsLock = new();

    /// <summary>
    /// PowerLauncher main executable
    /// </summary>
    private static readonly string? _powerLauncherExe = Path.GetFileName(Environment.ProcessPath);

    /// <summary>
    /// List of all the open windows
    /// </summary>
    private readonly List<Window> windows = new();

    /// <summary>
    /// An instance of the class OpenWindows
    /// </summary>
    private static OpenWindows? instance;

    /// <summary>
    /// Gets the list of all open windows
    /// </summary>
    internal List<Window> Windows => new(windows);

    /// <summary>
    /// Gets an instance property of this class that makes sure that
    /// the first instance gets created and that all the requests
    /// end up at that one instance
    /// </summary>
    internal static OpenWindows Instance
    {
        get
        {
            instance ??= new OpenWindows();

            return instance;
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenWindows"/> class.
    /// Private constructor to make sure there is never
    /// more than one instance of this class
    /// </summary>
    private OpenWindows()
    {
    }

    /// <summary>
    /// Updates the list of open windows
    /// </summary>
    internal void UpdateOpenWindowsList(CancellationToken cancellationToken)
    {
        var tokenHandle = GCHandle.Alloc(cancellationToken);
        try
        {
            var tokenHandleParam = GCHandle.ToIntPtr(tokenHandle);
            lock (_enumWindowsLock)
            {
                windows.Clear();
                EnumWindowsProc callbackptr = new EnumWindowsProc(WindowEnumerationCallBack);
                _ = NativeMethods.EnumWindows(callbackptr, tokenHandleParam);
            }
        }
        finally
        {
            if (tokenHandle.IsAllocated)
            {
                tokenHandle.Free();
            }
        }
    }

    /// <summary>
    /// Call back method for window enumeration
    /// </summary>
    /// <param name="hwnd">The handle to the current window being enumerated</param>
    /// <param name="lParam">Value being passed from the caller (we don't use this but might come in handy
    /// in the future</param>
    /// <returns>true to make sure to continue enumeration</returns>
    internal bool WindowEnumerationCallBack(IntPtr hwnd, IntPtr lParam)
    {
        var tokenHandle = GCHandle.FromIntPtr(lParam);
        var target = (CancellationToken?)tokenHandle.Target ?? CancellationToken.None;
        var cancellationToken = target;
        if (cancellationToken.IsCancellationRequested)
        {
            // Stop enumeration
            return false;
        }

        Window newWindow = new Window(hwnd);

        if (newWindow.IsWindow && newWindow.Visible && newWindow.IsOwner &&
            (!newWindow.IsToolWindow || newWindow.IsAppWindow) && !newWindow.TaskListDeleted &&
            (newWindow.Desktop.IsVisible || !SettingsManager.Instance.ResultsFromVisibleDesktopOnly || WindowWalkerCommandsProvider.VirtualDesktopHelperInstance.GetDesktopCount() < 2) &&
            newWindow.ClassName != "Windows.UI.Core.CoreWindow" && newWindow.Process.Name != _powerLauncherExe)
        {
            // To hide (not add) preloaded uwp app windows that are invisible to the user and other cloaked windows, we check the cloak state. (Issue #13637.)
            // (If user asking to see cloaked uwp app windows again we can add an optional plugin setting in the future.)
            if (!newWindow.IsCloaked || newWindow.GetWindowCloakState() == Window.WindowCloakState.OtherDesktop)
            {
                windows.Add(newWindow);
            }
        }

        return true;
    }
}
