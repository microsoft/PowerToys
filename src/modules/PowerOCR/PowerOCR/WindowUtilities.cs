using System.Collections.Generic;
using System.Windows;
using System.Windows.Forms;

namespace PowerOCR.Utilities;

public static class WindowUtilities
{
    public static void LaunchOCROverlayOnEveryScreen()
    {
        Screen[] allScreens = Screen.AllScreens;
        WindowCollection allWindows = System.Windows.Application.Current.Windows;

        List<OCROverlay> allFullscreenGrab = new();

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
                screenHasWindow = false;

            OCROverlay overlay = new()
            {
                WindowStartupLocation = WindowStartupLocation.Manual,
                Width = 200,
                Height = 200,
                WindowState = WindowState.Normal
            };

            if (screen.WorkingArea.Left >= 0)
                overlay.Left = screen.WorkingArea.Left;
            else
                overlay.Left = screen.WorkingArea.Left + (screen.WorkingArea.Width / 2);

            if (screen.WorkingArea.Top >= 0)
                overlay.Top = screen.WorkingArea.Top;
            else
                overlay.Top = screen.WorkingArea.Top + (screen.WorkingArea.Height / 2);

            overlay.Show();
            overlay.Activate();
            allFullscreenGrab.Add(overlay);
        }
    }

    internal static void CloseAllOCROverlays()
    {
        WindowCollection allWindows = System.Windows.Application.Current.Windows;

        foreach (Window window in allWindows)
            if (window is OCROverlay overlay)
                overlay.Close();

        System.Windows.Application.Current.Shutdown();
    }
}
