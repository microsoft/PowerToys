// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

using ManagedCommon;
using Microsoft.PowerToys.Telemetry;
using Microsoft.UI.Windowing;
using Windows.Graphics;
using Windows.System;

namespace PowerOCR.Utilities;

public static class WindowUtilities
{
    [DllImport("Shcore.dll")]
    private static extern IntPtr GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

    [DllImport("User32.dll")]
    private static extern IntPtr MonitorFromPoint(System.Drawing.Point pt, uint dwFlags);

    public static void LaunchOCROverlayOnEveryScreen()
    {
        if (IsOCROverlayCreated())
        {
            Logger.LogWarning("Tried to launch the overlay, but it has been already created.");
            return;
        }

        Logger.LogInfo($"Adding Overlays for each screen");
        var displays = DisplayArea.FindAll();
        foreach (var display in displays)
        {
            var outerBounds = display.OuterBounds;
            var screenRect = new RectInt32(outerBounds.X, outerBounds.Y, outerBounds.Width, outerBounds.Height);

            // Get DPI for this monitor
            var monitorPoint = new System.Drawing.Point(outerBounds.X + 1, outerBounds.Y + 1);
            var hMonitor = MonitorFromPoint(monitorPoint, 2 /* MONITOR_DEFAULTTONEAREST */);
            GetDpiForMonitor(hMonitor, 0 /* Effective */, out uint dpiX, out _);
            double scale = dpiX / 96.0;

            Logger.LogInfo($"display {display.DisplayId}, scale {scale}");
            OCROverlay overlay = new(screenRect, scale);

            overlay.Activate();
            ActivateWindow(overlay);
            App.TrackOverlay(overlay);
        }

        PowerToysTelemetry.Log.WriteEvent(new PowerOCR.Telemetry.PowerOCRInvokedEvent());
    }

    internal static bool IsOCROverlayCreated()
    {
        return App.Overlays.Count > 0;
    }

    internal static void CloseAllOCROverlays()
    {
        // Copy to avoid modification during iteration
        var overlays = new System.Collections.Generic.List<OCROverlay>(App.Overlays);

        foreach (var overlay in overlays)
        {
            overlay.Close();
        }

        GC.Collect();
    }

    public static void ActivateWindow(Microsoft.UI.Xaml.Window window)
    {
        var handle = WinRT.Interop.WindowNative.GetWindowHandle(window);
        var fgHandle = OSInterop.GetForegroundWindow();

        var threadId1 = OSInterop.GetWindowThreadProcessId(handle, IntPtr.Zero);
        var threadId2 = OSInterop.GetWindowThreadProcessId(fgHandle, IntPtr.Zero);

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

    internal static void OcrOverlayKeyDown(VirtualKey key, bool? isActive = null)
    {
        if (key == VirtualKey.Escape)
        {
            PowerToysTelemetry.Log.WriteEvent(new PowerOCR.Telemetry.PowerOCRCancelledEvent());
            CloseAllOCROverlays();
        }

        foreach (var overlay in App.Overlays)
        {
            overlay.KeyPressed(key, isActive);
        }
    }
}
