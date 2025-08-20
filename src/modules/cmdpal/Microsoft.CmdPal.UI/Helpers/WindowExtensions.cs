// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;

namespace Microsoft.CmdPal.UI.Helpers;

public static class WindowExtensions
{
    public static void SetIcon(this Window window)
    {
        var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        WindowId windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
        AppWindow appWindow = AppWindow.GetFromWindowId(windowId);
        appWindow.SetIcon(@"Assets\icon.ico");
    }

    public static void SetVisibilityInSwitchers(this Window window, bool showInSwitchers)
    {
        try
        {
            // IsShownInSwitchers needs to change the value to apply the effect, but its state might be out-of-sync with
            // the actual state of the switchers, so we need to toggle it.
            window.AppWindow.IsShownInSwitchers = !showInSwitchers;
            window.AppWindow.IsShownInSwitchers = showInSwitchers;
        }
        catch (NotImplementedException)
        {
            // Setting IsShownInSwitchers failed. This can happen if the Explorer is not running.
        }
    }
}
