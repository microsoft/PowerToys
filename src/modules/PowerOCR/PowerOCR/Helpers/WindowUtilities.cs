// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Windows;
using System.Windows.Forms;
using Microsoft.PowerToys.Telemetry;
using PowerOCR.Helpers;

namespace PowerOCR.Utilities;

public static class WindowUtilities
{
    public static void LaunchOCROverlayOnEveryScreen()
    {
        if (IsOCROverlayCreated())
        {
            Logger.LogWarning("Tired to launch the overlay but it was already created.");
            return;
        }

        Screen[] allScreens = Screen.AllScreens;
        WindowCollection allWindows = System.Windows.Application.Current.Windows;

        List<OCROverlay> allFullscreenGrab = new List<OCROverlay>();

        foreach (Screen screen in allScreens)
        {
            bool screenHasWindow = true;

            foreach (Window window in allWindows)
            {
                System.Drawing.Point windowCenter =
                    new System.Drawing.Point(
                        (int)(window.Left + (window.Width / 2)),
                        (int)(window.Top + (window.Height / 2)));
                screenHasWindow = screen.Bounds.Contains(windowCenter);

                // if (window is EditTextWindow)
                //     isEditWindowOpen = true;
            }

            if (allWindows.Count < 1)
            {
                screenHasWindow = false;
            }

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
            overlay.Activate();
            allFullscreenGrab.Add(overlay);
        }

        PowerToysTelemetry.Log.WriteEvent(new PowerOCR.Telemetry.PowerOCRLaunchOverlayEvent());
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
}
