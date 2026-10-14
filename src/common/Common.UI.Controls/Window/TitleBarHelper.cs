// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Windowing;

namespace Microsoft.PowerToys.Common.UI.Controls.Window;

/// <summary>
/// Helpers for configuring a window's system title bar.
/// </summary>
public static class TitleBarHelper
{
    /// <summary>
    /// Sets the title bar theme to the Windows default app mode.
    /// </summary>
    public static void SetPreferredTheme(global::Microsoft.UI.Xaml.Window? window)
    {
        if (window?.AppWindow is not null && AppWindowTitleBar.IsCustomizationSupported())
        {
            window.AppWindow.TitleBar.PreferredTheme = TitleBarTheme.UseDefaultAppMode;
        }
    }
}
