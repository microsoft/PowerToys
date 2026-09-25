// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Button = Microsoft.PowerToys.UITest.Next.Button;

namespace AdvancedPaste.UITests;

internal static class TestWindow
{
    internal static void SelectFromTaskbar(IntPtr handle, string processName)
    {
        if (WindowControl.GetForegroundWindowHandle() != handle)
        {
            var taskbar = WindowsFinder.WaitForWindow(window => window.ClassName == "Shell_TrayWnd");
            Assert.IsNotNull(taskbar, "The primary taskbar was not available.");
            var buttons = taskbar.FindAll<Button>(By.Name(processName), 10_000);
            Assert.HasCount(1, buttons, $"{processName} did not expose one taskbar button.");
            var button = buttons[0];
            Assert.IsTrue(button.Width > 0 && button.Height > 0 && !button.IsOffscreen, $"{processName}'s taskbar button is not visible.");
            // Clicking an already-active taskbar button minimizes it. Recheck after UIA lookup.
            if (WindowControl.GetForegroundWindowHandle() != handle)
            {
                MouseHelper.LeftClickAt(button.X + (button.Width / 2), button.Y + (button.Height / 2));
            }
        }

        var selected = WaitHelper.WaitForStable(
            WindowControl.GetForegroundWindowHandle,
            foreground => foreground == handle,
            timeoutMS: 10_000,
            requiredConsecutiveMatches: 2);
        Assert.IsTrue(
            selected.Succeeded,
            $"{processName} did not become foreground after taskbar selection. Actual: {WindowControl.GetForegroundWindowInfo()}.");
    }
}
