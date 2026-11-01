// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Text.Json;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OOBE.UITests;

internal static class OobeTestHelper
{
    internal const string WelcomeTitle = "Welcome to PowerToys";
    internal const string WhatsNewTitle = "What's new in PowerToys";
    internal const string SettingsProcess = "PowerToys.Settings";

    internal static Session WaitForSettingsWindow(string title)
    {
        var window = WindowsFinder.WaitForWindowByApp(
            SettingsProcess,
            w => string.Equals(w.Title, title, StringComparison.OrdinalIgnoreCase),
            timeoutMS: 150_000);
        Assert.IsNotNull(window, $"Settings did not present '{title}'.");
        WindowHelper.MaximizeWindow(new IntPtr(window.WindowHandle));
        return window;
    }

    internal static Session OpenWelcome(Session settings)
    {
        settings.Find<NavigationViewItem>(By.AccessibilityId("GeneralNavItem")).Invoke(msPostAction: 0);
        Session? welcome = null;
        for (var attempt = 0; attempt < 3 && welcome is null; attempt++)
        {
            var link = settings.Find<NavigationViewItem>(By.AccessibilityId("OOBENavItem"));
            link.ScrollIntoView();
            RequireForeground(settings);

            // Tapped opens OOBE idempotently; a cold window can drop the first physical click.
            link.MouseClick(msPostAction: 0);
            welcome = WindowsFinder.WaitForWindowByApp(
                SettingsProcess,
                window => string.Equals(window.Title, WelcomeTitle, StringComparison.OrdinalIgnoreCase),
                timeoutMS: 10_000);
        }

        Assert.IsNotNull(welcome, "The Settings Welcome link did not open OOBE after three attempts.");
        WindowHelper.MaximizeWindow(new IntPtr(welcome.WindowHandle));
        Assert.IsTrue(welcome.Has(By.AccessibilityId("WelcomeTitle"), 30_000), "The Getting started page was not ready.");
        return welcome;
    }

    internal static void SelectSection(Session welcome, string section)
    {
        var item = welcome.FindAll<NavigationViewItem>(By.Name(section), timeoutMS: 30_000)
            .Single(i => string.Equals(i.Name, section, StringComparison.OrdinalIgnoreCase));
        item.Invoke(msPostAction: 0);
        Assert.IsTrue(item.WaitForProperty("IsSelected", "True", 15_000), $"OOBE did not select '{section}'.");
        Assert.IsTrue(
            welcome.Find<TextBlock>(By.AccessibilityId("TitleTxt"), 30_000).WaitForValue(section, timeoutMS: 15_000),
            $"OOBE did not render the '{section}' page.");
    }

    internal static void CloseWindow(Session window)
    {
        Assert.IsTrue(
            WindowControl.TryCloseByApp(window.ProcessName, w => w.Hwnd == window.WindowHandle, timeoutMS: 15_000),
            $"'{window.WindowTitle}' did not close after WM_CLOSE.");
        Assert.IsTrue(
            WaitHelper.WaitForStable(
                () => !WindowControl.EnumerateAllWindows().Any(w => w.Hwnd.ToInt64() == window.WindowHandle && w.IsVisible),
                matches => matches,
                timeoutMS: 15_000).Succeeded,
            $"'{window.WindowTitle}' remained visible after closing.");
    }

    internal static void StopPowerToys()
    {
        Assert.IsTrue(WindowControl.TryKillProcessTreeByNameAndWait(SettingsProcess, 30_000), "Settings did not stop.");
        Assert.IsTrue(WindowControl.TryKillProcessTreeByNameAndWait("PowerToys", 30_000), "Runner did not stop.");
    }

    internal static void LaunchRunner(string arguments = "")
    {
        var executable = SessionHelper.GetExecutablePath(PowerToysModule.Runner);
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = executable,
            Arguments = arguments,
            WorkingDirectory = Path.GetDirectoryName(executable)!,
            UseShellExecute = true,
        });
        Assert.IsNotNull(process, "Could not start Runner without an explicit Settings request.");
    }

    internal static void RequireForeground(Session window)
    {
        var handle = new IntPtr(window.WindowHandle);
        if (!WindowControl.WaitForForeground(handle, timeoutMS: 1_000))
        {
            // Activate through real input when a toast refuses the programmatic foreground handoff.
            var caption = window.FindAll<TextBlock>(By.Name(window.WindowTitle), 5_000)
                .Where(text => text.Width > 0 && text.Height > 0)
                .OrderBy(text => text.Y)
                .First();
            caption.Click(msPostAction: 0);
        }

        Assert.IsTrue(
            WindowControl.WaitForForeground(handle, timeoutMS: 15_000, requiredConsecutiveMatches: 2),
            $"'{window.WindowTitle}' could not own foreground: {WindowControl.GetForegroundWindowInfo()}.");
    }

    internal static JsonDocument ReadJson(string path)
    {
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        return JsonDocument.Parse(file);
    }
}
