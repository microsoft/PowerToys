// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
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
    private static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

    [DllImport("User32.dll")]
    private static extern IntPtr MonitorFromPoint(long pt, uint dwFlags);

    public static void LaunchOCROverlayOnEveryScreen()
    {
        if (IsOCROverlayCreated())
        {
            Logger.LogWarning("Tried to launch the overlay, but it has been already created.");
            return;
        }

        Logger.LogInfo($"Adding Overlays for each screen");

        IReadOnlyList<DisplayArea> displays;
        try
        {
            displays = DisplayArea.FindAll();
            Logger.LogInfo($"Found {displays.Count} displays");
        }
        catch (Exception ex)
        {
            Logger.LogError($"DisplayArea.FindAll() failed: {ex}");
            return;
        }

        for (int i = 0; i < displays.Count; i++)
        {
            try
            {
                var display = displays[i];
                Logger.LogInfo("Step 1: getting OuterBounds");
                var outerBounds = display.OuterBounds;
                Logger.LogInfo($"Step 2: bounds={outerBounds.X},{outerBounds.Y},{outerBounds.Width},{outerBounds.Height}");
                var screenRect = new RectInt32(outerBounds.X, outerBounds.Y, outerBounds.Width, outerBounds.Height);

                // Get DPI for this monitor
                Logger.LogInfo("Step 3: MonitorFromPoint");
                long packedPoint = (long)(outerBounds.X + 1) | ((long)(outerBounds.Y + 1) << 32);
                var hMonitor = MonitorFromPoint(packedPoint, 2 /* MONITOR_DEFAULTTONEAREST */);
                Logger.LogInfo("Step 4: GetDpiForMonitor");
                int hr = GetDpiForMonitor(hMonitor, 0 /* Effective */, out uint dpiX, out _);
                double scale = hr == 0 ? dpiX / 96.0 : 1.0;

                Logger.LogInfo($"Step 5: display {display.DisplayId}, scale {scale}");
                Logger.LogInfo("Step 6: Creating OCROverlay...");
                OCROverlay overlay = new(screenRect, scale);
                Logger.LogInfo("Step 7: OCROverlay created, activating...");

                overlay.Activate();
                ActivateWindow(overlay);
                App.TrackOverlay(overlay);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to create overlay for display: {ex}");
            }
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
