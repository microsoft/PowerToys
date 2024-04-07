// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using ManagedCommon;
using Microsoft.PowerToys.Telemetry;

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

        Logger.LogInfo($"Adding Overlays for each screen");
        foreach (Screen screen in Screen.AllScreens)
        {
            DpiScale dpiScale = screen.GetDpi();
            Logger.LogInfo($"screen {screen}, dpiScale {dpiScale.DpiScaleX}, {dpiScale.DpiScaleY}");
            OCROverlay overlay = new(screen.Bounds, dpiScale);

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
            if (window is OCROverlay)
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

        GC.Collect();

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

    internal static void OcrOverlayKeyDown(Key key, bool? isActive = null)
    {
        WindowCollection allWindows = System.Windows.Application.Current.Windows;

        if (key == Key.Escape)
        {
            PowerToysTelemetry.Log.WriteEvent(new PowerOCR.Telemetry.PowerOCRCancelledEvent());
            CloseAllOCROverlays();
        }

        foreach (Window window in allWindows)
        {
            if (window is OCROverlay overlay)
            {
                overlay.KeyPressed(key, isActive);
            }
        }
    }
}
