// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.UI.Windowing;
using Windows.Graphics;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace Microsoft.CmdPal.UI.Helpers;

internal static class MonitorHelper
{
    /// <summary>
    /// Gets the display area based on the specified monitor behavior.
    /// </summary>
    /// <param name="hwnd"></param>
    /// <param name="target"></param>
    /// <returns></returns>
    private static DisplayArea GetScreen(HWND hwnd, MonitorBehavior target)
    {
        if (hwnd.IsNull)
        {
            throw new ArgumentNullException(nameof(hwnd), "Window handle cannot be null.");
        }

        // Leaving a note here, in case we ever need it:
        // https://github.com/microsoft/microsoft-ui-xaml/issues/6454
        // If we need to ever FindAll, we'll need to iterate manually
        switch (target)
        {
            case MonitorBehavior.InPlace:
                if (PInvoke.GetWindowRect(hwnd, out var bounds))
                {
                    RectInt32 converted = new(bounds.X, bounds.Y, bounds.Width, bounds.Height);
                    return DisplayArea.GetFromRect(converted, DisplayAreaFallback.Nearest);
                }

                break;

            case MonitorBehavior.ToFocusedWindow:
                var foregroundWindowHandle = PInvoke.GetForegroundWindow();
                if (foregroundWindowHandle != IntPtr.Zero)
                {
                    if (PInvoke.GetWindowRect(foregroundWindowHandle, out var fgBounds))
                    {
                        RectInt32 converted = new(fgBounds.X, fgBounds.Y, fgBounds.Width, fgBounds.Height);
                        return DisplayArea.GetFromRect(converted, DisplayAreaFallback.Nearest);
                    }
                }

                break;

            case MonitorBehavior.ToPrimary:
                return DisplayArea.Primary;

            case MonitorBehavior.ToMouse:
            default:
                if (PInvoke.GetCursorPos(out var cursorPos))
                {
                    return DisplayArea.GetFromPoint(new PointInt32(cursorPos.X, cursorPos.Y), DisplayAreaFallback.Nearest);
                }

                break;
        }

        return DisplayArea.Primary;
    }

    internal static void PositionCentered(HWND hwnd, AppWindow appWindow, MonitorBehavior monitorBehavior)
    {
        var displayArea = GetScreen(hwnd, monitorBehavior);
        PositionCentered(appWindow, displayArea);
    }

    internal static void PositionCentered(AppWindow appWindow, DisplayAreaFallback displayAreaFallback = DisplayAreaFallback.Nearest)
    {
        var displayArea = DisplayArea.GetFromWindowId(appWindow.Id, displayAreaFallback);
        PositionCentered(appWindow, displayArea);
    }

    internal static void PositionCentered(AppWindow appWindow, DisplayArea displayArea)
    {
        if (displayArea is not null)
        {
            var centeredPosition = appWindow.Position;
            centeredPosition.X = (displayArea.WorkArea.Width - appWindow.Size.Width) / 2;
            centeredPosition.Y = (displayArea.WorkArea.Height - appWindow.Size.Height) / 2;

            centeredPosition.X += displayArea.WorkArea.X;
            centeredPosition.Y += displayArea.WorkArea.Y;
            appWindow.Move(centeredPosition);
        }
    }
}
