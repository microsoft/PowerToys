// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Windows;
using System.Windows.Forms;
using ManagedCommon;
using Microsoft.PowerToys.Telemetry;
using PowerOCR.Helpers;

namespace PowerOCR.Utilities;

public static class WindowUtilities
{
    public static void LaunchOCROverlayOnEveryScreen()
    {
        if (IsOCROverlayCreated())
        {
            Logger.LogWarning("Tried to launch the overlay, but it has been already created.");
            return;
        }

        foreach (Screen screen in Screen.AllScreens)
        {
            OCROverlay overlay = new OCROverlay()
            {
                WindowStartupLocation = WindowStartupLocation.Manual,
                Width = 200,
                Height = 200,
                WindowState = WindowState.Normal,
            };

            if (screen.WorkingArea.Left >= 0)
            {
                overlay.Left = screen.WorkingArea.Left;
            }
            else
            {
                overlay.Left = screen.WorkingArea.Left + (screen.WorkingArea.Width / 2);
            }

            if (screen.WorkingArea.Top >= 0)
            {
                overlay.Top = screen.WorkingArea.Top;
            }
            else
            {
                overlay.Top = screen.WorkingArea.Top + (screen.WorkingArea.Height / 2);
            }

            overlay.Show();
            ActivateWindow(overlay);
        }

        PowerToysTelemetry.Log.WriteEvent(new PowerOCR.Telemetry.PowerOCRInvokedEvent());
    }

    internal static bool IsOCROverlayCreated()
    {
        WindowCollection allWindows = System.Windows.Application.Current.Windows;

        foreach (Window window in allWindows)
        {
            if (window is OCROverlay overlay)
            {
                return true;
            }
        }

        return false;
    }

    internal static void CloseAllOCROverlays()
    {
        WindowCollection allWindows = System.Windows.Application.Current.Windows;

        foreach (Window window in allWindows)
        {
            if (window is OCROverlay overlay)
            {
                overlay.Close();
            }
        }

        // TODO: Decide when to close the process
        // System.Windows.Application.Current.Shutdown();
    }

    public static void ActivateWindow(Window window)
    {
        var handle = new System.Windows.Interop.WindowInteropHelper(window).Handle;
        var fgHandle = OSInterop.GetForegroundWindow();

        var threadId1 = OSInterop.GetWindowThreadProcessId(handle, System.IntPtr.Zero);
        var threadId2 = OSInterop.GetWindowThreadProcessId(fgHandle, System.IntPtr.Zero);

        if (threadId1 != threadId2)
        {
            OSInterop.AttachThreadInput(threadId1, threadId2, true);
            OSInterop.SetForegroundWindow(handle);
            OSInterop.AttachThreadInput(threadId1, threadId2, false);
        }
        else
        {
            OSInterop.SetForegroundWindow(handle);
        }
    }
}
