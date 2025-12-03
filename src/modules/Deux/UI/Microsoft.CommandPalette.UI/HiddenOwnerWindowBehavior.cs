// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.UI.Helpers;
using Microsoft.UI.Xaml;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Microsoft.CommandPalette.UI;

/// <summary>
/// Provides behavior to control taskbar and Alt+Tab presence by assigning a hidden owner
/// and toggling extended window styles for a target window.
/// </summary>
internal sealed class HiddenOwnerWindowBehavior
{
    private HWND _hiddenOwnerHwnd;
    private Window? _hiddenWindow;

    /// <summary>
    /// Shows or hides a window in the taskbar (and Alt+Tab) by updating ownership and extended window styles.
    /// </summary>
    /// <param name="target">The <see cref="Microsoft.UI.Xaml.Window"/> to update.</param>
    /// <param name="isVisibleInTaskbar"> True to show the window in the taskbar (and Alt+Tab); false to hide it from both. </param>
    /// <remarks>
    /// When hiding the window, a hidden owner is assigned and <see cref="WINDOW_EX_STYLE.WS_EX_TOOLWINDOW"/>
    /// is enabled to keep it out of the taskbar and Alt+Tab. When showing, the owner is cleared and
    /// <see cref="WINDOW_EX_STYLE.WS_EX_APPWINDOW"/> is enabled to ensure taskbar presence. Since tool
    /// windows use smaller corner radii, the normal rounded corners are enforced via
    /// <see cref="DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_ROUND"/>.
    /// </remarks>
    /// <seealso href="https://learn.microsoft.com/en-us/windows/win32/shell/taskbar#managing-taskbar-buttons" />
    public void ShowInTaskbar(Window target, bool isVisibleInTaskbar)
    {
        /*
         * There are the three main ways to control whether a window appears on the taskbar:
         * https://learn.microsoft.com/en-us/windows/win32/shell/taskbar#managing-taskbar-buttons
         *
         * 1. Set the window's owner. Owned windows do not appear on the taskbar:
         *    Turns out this is the most reliable way to hide a window from the taskbar and ALT+TAB. WinForms and WPF uses this method
         *    to back their ShowInTaskbar property as well.
         *
         * 2. Use the WS_EX_TOOLWINDOW extended window style:
         *    This mostly works, with some reports that it silently fails in some cases. The biggest issue
         *    is that for certain Windows settings (like Multitasking -> Show taskbar buttons on all displays = On all desktops),
         *    the taskbar button is always shown even for tool windows.
         *
         * 3. Using ITaskbarList:
         *    This is what AppWindow.IsShownInSwitchers uses, but it's COM-based and more complex, and can
         *    fail if Explorer isn't running or responding. It could be a good backup, if needed.
         */

        var visibleHwnd = target.GetWindowHwnd();

        if (isVisibleInTaskbar)
        {
            // remove any owner window
            PInvoke.SetWindowLongPtr(visibleHwnd, WINDOW_LONG_PTR_INDEX.GWLP_HWNDPARENT, HWND.Null);
        }
        else
        {
            // Set the hidden window as the owner of the target window
            var hiddenHwnd = EnsureHiddenOwner();
            PInvoke.SetWindowLongPtr(visibleHwnd, WINDOW_LONG_PTR_INDEX.GWLP_HWNDPARENT, hiddenHwnd);
        }

        // Tool windows don't show up in ALT+TAB, and don't show up in the taskbar
        // Tool window and app window styles are mutually exclusive, change both just to be safe
        target.ToggleExtendedWindowStyle(WINDOW_EX_STYLE.WS_EX_TOOLWINDOW, !isVisibleInTaskbar);
        target.ToggleExtendedWindowStyle(WINDOW_EX_STYLE.WS_EX_APPWINDOW, isVisibleInTaskbar);

        // Since tool windows have smaller corner radii, we need to force the normal ones
        target.SetCornerPreference(DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_ROUND);
    }

    private HWND EnsureHiddenOwner()
    {
        if (_hiddenOwnerHwnd.IsNull)
        {
            _hiddenWindow = new Window();
            _hiddenOwnerHwnd = _hiddenWindow.GetWindowHwnd();
        }

        return _hiddenOwnerHwnd;
    }
}
