// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerDisplay.UITests;

internal static class PowerDisplayTray
{
    private static readonly string[] ChevronNames = ["hidden icon", "Notification Chevron", "NotificationChevron"];

    internal static void ParkCursor()
    {
        var display = MonitorInfo.GetPrimary()!;
        MouseHelper.MoveTo(display.Left + (display.Width / 4), display.Top + (display.Height / 2));
    }

    internal static Element FindIcon()
    {
        ShellMenu.Dismiss();
        KeyboardHelper.SendKeys(Key.LWin, Key.B);
        ParkCursor();
        var taskbar = WindowsFinder.WaitForWindowByApp("explorer", window => window.ClassName == "Shell_TrayWnd");
        Assert.IsNotNull(taskbar, "Explorer did not expose its primary taskbar.");
        Stopwatch? lastChevronClick = null;
        WindowsFinder.WindowInfo? Overflow() => WindowsFinder.ListByApp("explorer").FirstOrDefault(window =>
            window.ClassName is "NotifyIconOverflowWindow" or "TopLevelWindowForOverflowXamlIsland" &&
            !WindowHelper.IsWindowCloaked(new IntPtr(window.Hwnd)));
        Session? OverflowSession() => Overflow() is { } window
            ? WindowsFinder.WaitForWindowByApp("explorer", candidate => candidate.Hwnd == window.Hwnd, timeoutMS: 500)
            : null;

        // A clean desktop has a taskbar but no ordinary Explorer app window for CLI -a matching.
        var result = WaitHelper.WaitForStable(
            () => new[] { taskbar, OverflowSession() }.OfType<Session>()
                .SelectMany(surface => surface.FindAll<Element>(By.Name("Power Display"), 0))
                .Where(element => element.Name == "Power Display" && element.ControlType == "Button" &&
                    element.Width > 0 && element.Height > 0 && element.Displayed)
                .DistinctBy(element => element.Selector).ToArray(),
            icons => icons is { Length: 1 } && WindowsFinder.ListByApp("explorer").Any(window =>
                window.ClassName is "Shell_TrayWnd" or "Shell_SecondaryTrayWnd" or "NotifyIconOverflowWindow" or "TopLevelWindowForOverflowXamlIsland" &&
                WindowControl.IsPointOwnedByWindow(new IntPtr(window.Hwnd), icons[0].X + (icons[0].Width / 2), icons[0].Y + (icons[0].Height / 2))),
            30_000,
            2,
            200,
            recover: icons =>
            {
                if (icons is { Length: > 1 })
                {
                    // Explorer retains icons from forcibly stopped processes until it hit-tests them.
                    foreach (var icon in icons)
                    {
                        MouseHelper.MoveTo(icon.X + (icon.Width / 2), icon.Y + (icon.Height / 2));
                        Thread.Sleep(150); // Let Explorer process each hover instead of coalescing the moves.
                    }

                    ParkCursor();
                }

                if (Overflow() is { } overflow)
                {
                    WindowControl.TryBringToForeground(new IntPtr(overflow.Hwnd));
                }
                else if (lastChevronClick is null || lastChevronClick.ElapsedMilliseconds >= 3_000)
                {
                    var chevron = ChevronNames
                        .SelectMany(name => taskbar.FindAll<Element>(By.Name(name), 0))
                        .FirstOrDefault(element => element.ControlType == "Button" && element.Width > 0 && element.Height > 0 && element.Displayed);
                    if (chevron is not null)
                    {
                        MouseHelper.LeftClickAt(chevron.X + (chevron.Width / 2), chevron.Y + (chevron.Height / 2));
                        lastChevronClick = Stopwatch.StartNew();
                        ParkCursor();
                    }
                }
            },
            shouldRetryException: ShellMenu.IsTransientElementException);
        if (!result.Succeeded)
        {
            Assert.Fail($"The Shell did not expose one hit-testable Power Display notification icon. Last transient error: {result.LastException}. Taskbar: {taskbar.Inspect(depth: 12)}. Overflow: {OverflowSession()?.Inspect(depth: 12)}");
        }

        return result.LastObservation!.Single();
    }
}
