// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using ManagedCommon;
using Microsoft.PowerToys.Telemetry;
using PowerOCR.Helpers;
using PowerOCR.NativeMethods;

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

        var screens = ScreenHelper.GetAllScreens().ToList();
        var dpiScales = screens.Select(
                screen =>
                {
                    var dpiX = new Core.UINT(0);
                    var dpiY = new Core.UINT(0);
                    var apiResult = PInvoke.GetDpiForMonitor(
                        hmonitor: new(screen.Handle),
                        dpiType: 0,
                        dpiX: ref dpiX,
                        dpiY: ref dpiY);

                    if (apiResult != 0)
                    {
                        throw new InvalidOperationException();
                    }

                    return new DpiScale(dpiX.Value / 96.0, dpiY.Value / 96.0);
                })
            .ToList();

        List<OCROverlay> overlays = screens.Zip(dpiScales, (screen, dpiScale) => (Screen: screen, DpiScale: dpiScale))
            .Select(
                item =>
                {
                    var overlay = new OCROverlay
                    {
                        CurrentScreen = item.Screen,
                        CurrentScaling = item.DpiScale,
                    };

                    // get the system dpi settings (i.e. the scaling for the *primary* monitor)
                    var systemDpi = VisualTreeHelper.GetDpi(overlay);

                    // get the physical coordinates of the screen
                    var bounds = item.Screen.DisplayArea;

                    // WPF works internally with *system* scaling settings regardless of
                    // the process dpi awareness, so we need to convert physical pixel
                    // sizes to "Device-Independent Pixels" (DIPs) when setting the
                    // window size and position - that way we'll magically end up with
                    // the right coordinates when WPF scales them back up again
                    overlay.Left = bounds.Left * systemDpi.DpiScaleX / item.DpiScale.DpiScaleX;
                    overlay.Top = bounds.Top * systemDpi.DpiScaleY / item.DpiScale.DpiScaleY;
                    overlay.Width = bounds.Width * systemDpi.DpiScaleX / item.DpiScale.DpiScaleX;
                    overlay.Height = bounds.Height * systemDpi.DpiScaleY / item.DpiScale.DpiScaleY;

                    /*
                     * bit of a hack here trying to get the window to fit the screen
                     * It is slightly too large...and changing the width & height
                     * before calling Show() it doesn't actually change it
                     */

                    overlay.Opacity = 0;
                    overlay.Show();
                    overlay.Width -= 16;
                    overlay.Height -= 16;
                    overlay.Opacity = 1;
                    ActivateWindow(overlay);

                    return overlay;
                })
            .ToList();

        PowerToysTelemetry.Log.WriteEvent(new PowerOCR.Telemetry.PowerOCRInvokedEvent());
    }

    internal static bool IsOCROverlayCreated()
    {
        WindowCollection allWindows = Application.Current.Windows;

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
        WindowCollection allWindows = Application.Current.Windows;

        foreach (Window window in allWindows)
        {
            if (window is OCROverlay overlay)
            {
                overlay.Close();
            }
        }

        GC.Collect();
    }

    public static void ActivateWindow(Window window)
    {
        nint handle = new System.Windows.Interop.WindowInteropHelper(window).Handle;
        nint fgHandle = OSInterop.GetForegroundWindow();

        uint threadId1 = OSInterop.GetWindowThreadProcessId(handle, nint.Zero);
        uint threadId2 = OSInterop.GetWindowThreadProcessId(fgHandle, nint.Zero);

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
