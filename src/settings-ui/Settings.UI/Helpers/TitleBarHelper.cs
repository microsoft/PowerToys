// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI;
using Microsoft.UI.Xaml;
using Windows.UI;

namespace Microsoft.PowerToys.Settings.UI.Helpers
{
    /// <summary>
    /// Helpers for theming the system caption buttons (minimize/maximize/close) of a window.
    /// </summary>
    public static class TitleBarHelper
    {
        /// <summary>
        /// Applies the given element theme to a window's system caption buttons.
        /// </summary>
        /// <remarks>
        /// Workaround for the AppWindow TitleBar not updating caption button colors to match the
        /// app theme when the OS theme differs from the app theme or the theme changes at runtime.
        /// Mirrors the helper used by the WinUI Gallery (https://github.com/microsoft/WinUI-Gallery).
        /// </remarks>
        public static void ApplySystemThemeToCaptionButtons(Window window, ElementTheme theme)
        {
            if (window?.AppWindow is null)
            {
                return;
            }

            var titleBar = window.AppWindow.TitleBar;
            var foregroundColor = theme == ElementTheme.Dark ? Colors.White : Colors.Black;

            titleBar.ButtonBackgroundColor = Colors.Transparent;
            titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
            titleBar.ButtonForegroundColor = foregroundColor;
            titleBar.ButtonHoverForegroundColor = foregroundColor;
            titleBar.ButtonInactiveForegroundColor = Colors.DarkGray;
            titleBar.ButtonHoverBackgroundColor = theme == ElementTheme.Dark
                ? Color.FromArgb(24, 255, 255, 255)
                : Color.FromArgb(24, 0, 0, 0);
        }
    }
}
