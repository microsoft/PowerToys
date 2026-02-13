// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Microsoft.CmdPal.UI.Helpers;

internal static class WindowExtensions
{
    public static void SetIcon(this Window window)
    {
        var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        WindowId windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
        AppWindow appWindow = AppWindow.GetFromWindowId(windowId);
        appWindow.SetIcon(@"Assets\icon.ico");
    }

    public static HWND GetWindowHwnd(this Window window)
    {
        return window is null
            ? throw new ArgumentNullException(nameof(window))
            : new HWND(WinRT.Interop.WindowNative.GetWindowHandle(window));
    }

    /// <summary>
    /// Toggles the specified extended window style on or off for the supplied <see cref="Window"/>.
    /// </summary>
    /// <param name="window">The <see cref="Window"/> whose extended window styles will be modified. Cannot be null.</param>
    /// <param name="style">The <see cref="WINDOW_EX_STYLE"/> flag(s) to set or clear.</param>
    /// <param name="isStyleSet">When true, the specified <paramref name="style"/> bit(s) will be set (added). When false, the bit(s) will be cleared (removed).</param>
    /// <returns>True if the call to SetWindowLong succeeded and the style was applied; otherwise false.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="window"/> is null.</exception>
    internal static bool ToggleExtendedWindowStyle(this Window window, WINDOW_EX_STYLE style, bool isStyleSet)
    {
        var hWnd = GetWindowHwnd(window);
        var currentStyle = PInvoke.GetWindowLong(hWnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);

        if (isStyleSet)
        {
            currentStyle |= (int)style;
        }
        else
        {
            currentStyle &= ~(int)style;
        }

        var wasSet = PInvoke.SetWindowLong(hWnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE, currentStyle) != 0;

        // SWP_FRAMECHANGED - invalidate cached window style
        PInvoke.SetWindowPos(hWnd, new HWND(IntPtr.Zero), 0, 0, 0, 0, SET_WINDOW_POS_FLAGS.SWP_FRAMECHANGED | SET_WINDOW_POS_FLAGS.SWP_NOMOVE | SET_WINDOW_POS_FLAGS.SWP_NOSIZE | SET_WINDOW_POS_FLAGS.SWP_NOZORDER | SET_WINDOW_POS_FLAGS.SWP_NOOWNERZORDER);

        return wasSet;
    }

    /// <summary>
    /// Sets the window corner preference
    /// </summary>
    /// <param name="window">The window</param>
    /// <param name="cornerPreference">The desired corner preference</param>
    /// <returns>True if the operation succeeded</returns>
    public static bool SetCornerPreference(this Window window, DWM_WINDOW_CORNER_PREFERENCE cornerPreference)
    {
        return window.GetWindowHwnd().SetDwmWindowAttribute(DWMWINDOWATTRIBUTE.DWMWA_WINDOW_CORNER_PREFERENCE, cornerPreference);
    }

    /// <summary>
    /// Unified wrapper for DwmSetWindowAttribute calls with enum values
    /// </summary>
    private static bool SetDwmWindowAttribute<T>(this HWND hwnd, DWMWINDOWATTRIBUTE attribute, T value)
        where T : unmanaged, Enum
    {
        unsafe
        {
            var result = PInvoke.DwmSetWindowAttribute(hwnd, attribute, &value, (uint)sizeof(T));
            return result.Succeeded;
        }
    }
}
