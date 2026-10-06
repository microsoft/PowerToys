// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

using ManagedCommon;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;

using MouseJump.Common.Helpers;
using MouseJump.Common.Interop;
using MouseJump.Models.Display;
using MouseJump.Models.Drawing;
using MouseJump.WinUI3.Helpers;

using Windows.System;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.
namespace MouseJump.WinUI3.UI;

internal sealed partial class PreviewWindow
{
    /// <summary>
    /// Calculates the high-dpi scaling ratio based on the current monitor's display settings.
    /// </summary>
    private double GetHighDpiScalingRatio()
    {
        var hWnd = (HWND)WinRT.Interop.WindowNative.GetWindowHandle(this);
        var windowDpi = PInvoke.GetDpiForWindow(hWnd);
        ResultHandler.ThrowIfZero(result: (int)windowDpi, getLastError: true, memberName: nameof(PInvoke.GetDpiForWindow));
        var scalingRatio = (double)PInvoke.USER_DEFAULT_SCREEN_DPI / windowDpi;
        return scalingRatio;
    }

    internal async Task InvokeOnUiThreadAsync(Action action)
    {
        // this might be called from a task that we're awaiting
        // so we need to make sure we use the UI thread
        var tcs = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        if (!this.DispatcherQueue.TryEnqueue(
            () =>
            {
                try
                {
                    action();
                    tcs.SetResult(true);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            }))
        {
            tcs.SetException(
                new InvalidOperationException("DispatcherQueue is not accepting work items."));
        }

        // wait for the task to complete
        await tcs.Task.ConfigureAwait(false);
    }
}
