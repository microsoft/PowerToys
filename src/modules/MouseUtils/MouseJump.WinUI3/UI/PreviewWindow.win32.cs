// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

using Microsoft.UI.Windowing;

using MouseJump.Common.Interop;

using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.
namespace MouseJump.WinUI3.UI;

/// <summary>
/// An empty window that can be used on its own or navigated to within a Frame.
/// </summary>
internal sealed partial class PreviewWindow
{
    /// <summary>
    /// Initializes some settings on the application window.
    /// </summary>
    private void InitializeWindow()
    {
        var appWindow = this.AppWindow;
        var presenter = appWindow.Presenter as OverlappedPresenter;
        if (presenter != null)
        {
            var hWnd = (HWND)WinRT.Interop.WindowNative.GetWindowHandle(this);

            // get the current window style
            PInvoke.SetLastError(0);
            var result = PInvoke.GetWindowLong(hWnd, WINDOW_LONG_PTR_INDEX.GWL_STYLE);
            if (result == 0)
            {
                var lastError = Marshal.GetLastPInvokeError();
                ResultHandler.HandleResult(result: result, success: lastError == 0, lastError: lastError, memberName: nameof(PInvoke.GetWindowLong));
            }

            // set the window to be borderless, with no title bar, and hide all of the max / min / close buttons
            var style = (WINDOW_STYLE)result;
            style &= ~WINDOW_STYLE.WS_OVERLAPPEDWINDOW;
            style |= WINDOW_STYLE.WS_POPUP;
            PInvoke.SetLastError(0);
            result = PInvoke.SetWindowLong(hWnd, WINDOW_LONG_PTR_INDEX.GWL_STYLE, (int)style);
            if (result == 0)
            {
                var lastError = Marshal.GetLastPInvokeError();
                ResultHandler.HandleResult(result: result, success: lastError == 0, lastError: lastError, memberName: nameof(PInvoke.SetWindowLong));
            }

            // get the current extended window style
            PInvoke.SetLastError(0);
            result = PInvoke.GetWindowLong(hWnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
            if (result == 0)
            {
                var lastError = Marshal.GetLastPInvokeError();
                ResultHandler.HandleResult(result: result, success: lastError == 0, lastError: lastError, memberName: nameof(PInvoke.GetWindowLong));
            }

            // set the window to be borderless, with no title bar, and hide all of the max / min / close buttons
            var exStyle = (WINDOW_EX_STYLE)result;
            exStyle |= WINDOW_EX_STYLE.WS_EX_TOOLWINDOW; // hide the taskbar icon
            exStyle |= WINDOW_EX_STYLE.WS_EX_TOPMOST;    // make topmost
            PInvoke.SetLastError(0);
            result = PInvoke.SetWindowLong(hWnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE, (int)exStyle);
            if (result == 0)
            {
                var lastError = Marshal.GetLastPInvokeError();
                ResultHandler.HandleResult(result: result, success: lastError == 0, lastError: lastError, memberName: nameof(PInvoke.SetWindowLong));
            }
        }

        this.Activated += this.PreviewWindow_Activated;
        this.StackPanel.PreviewKeyDown += this.PreviewWindow_PreviewKeyDown;
        this.PreviewImage.PreviewKeyDown += this.PreviewWindow_PreviewKeyDown;
        this.PreviewImage.PointerPressed += this.PreviewImage_PointerPressed;
    }
}
