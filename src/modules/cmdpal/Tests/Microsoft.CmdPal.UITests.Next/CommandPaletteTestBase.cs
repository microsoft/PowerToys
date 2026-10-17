// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UITests;

public class CommandPaletteTestBase : UITestBase
{
    private Session? commandPaletteSession;

    public CommandPaletteTestBase()
        : base(PowerToysModule.CommandPalette)
    {
    }

    protected Session CommandPaletteSession =>
        commandPaletteSession ??= Session.FromProcess(
            "Microsoft.CmdPal.UI",
            PowerToysModule.CommandPalette,
            timeoutMS: 10_000);

    protected void SetSearchBox(string text)
    {
        EnsureCommandPaletteForeground();
        Step($"Setting Command Palette search to '{text}'");
        var searchBox = CommandPaletteSession.Find<TextBox>(By.AccessibilityId("MainSearchBox"));
        searchBox.SetText(text);

        var value = searchBox.Value;
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (value != text && DateTime.UtcNow < deadline)
        {
            Thread.Sleep(250);
            value = searchBox.Value;
        }

        Assert.AreEqual(text, value);
    }

    /// <summary>
    /// Command Palette dismisses itself whenever its window is deactivated. On a slow (1 vCPU) machine
    /// the cold-started window can lose activation before the test's first interaction, leaving it
    /// cloaked. Relaunching the executable redirects to the running instance, which summons the window.
    /// </summary>
    protected void EnsureCommandPaletteForeground()
    {
        const string processName = "Microsoft.CmdPal.UI";
        for (var attempt = 0; attempt < 3; attempt++)
        {
            if (WaitUntilForegroundProcess(processName, TimeSpan.FromSeconds(attempt == 0 ? 2 : 15)))
            {
                return;
            }

            var foreground = WindowControl.GetForegroundWindowInfo();
            Step($"Command Palette is not in the foreground (foreground: '{foreground.ProcessName}' '{foreground.Title}'); re-summoning");
            using (Process.Start(new ProcessStartInfo
            {
                FileName = SessionHelper.GetExecutablePath(PowerToysModule.CommandPalette),
                UseShellExecute = true,
            }))
            {
            }

            // The relaunched process only redirects its activation to the running instance and then
            // exits. Wait for it to go away so winapp's process-name target resolves to the real window.
            var redirectDeadline = DateTime.UtcNow.AddSeconds(30);
            while (Process.GetProcessesByName(processName).Length > 1 && DateTime.UtcNow < redirectDeadline)
            {
                Thread.Sleep(250);
            }
        }

        Assert.IsTrue(
            WaitUntilForegroundProcess(processName, TimeSpan.FromSeconds(15)),
            $"Command Palette did not become the foreground window. Foreground: '{WindowControl.GetForegroundWindowInfo().ProcessName}'.");
    }

    private static bool WaitUntilForegroundProcess(string processName, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        do
        {
            if (string.Equals(WindowControl.GetForegroundWindowInfo().ProcessName, processName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            Thread.Sleep(250);
        }
        while (DateTime.UtcNow < deadline);

        return false;
    }

    /// <summary>
    /// Double-click a result. Command Palette's double-tap handler invokes the list's current
    /// selection, so select the result first and wait until it is selected; otherwise, on a slow
    /// machine, the double-click can invoke a stale selection while the list is still updating.
    /// A late in-place result update can also reorder the list or reset the selection to the first
    /// item, so the result is re-resolved by its exact name on every step (an element's selector
    /// can go stale across such updates), must stay selected for a short settle window, and is
    /// reselected if the selection was reset.
    /// </summary>
    protected void DoubleClickResult(Element item) => DoubleClickResult(item.Name);

    protected void DoubleClickResult(string resultName)
    {
        var settle = TimeSpan.FromMilliseconds(1500);
        var deadline = DateTime.UtcNow.AddSeconds(20);
        DateTime? selectedSince = null;
        FindExactResult(resultName).Click();
        while (DateTime.UtcNow < deadline)
        {
            if (IsResultSelected(resultName))
            {
                selectedSince ??= DateTime.UtcNow;
                if (DateTime.UtcNow - selectedSince >= settle)
                {
                    break;
                }
            }
            else
            {
                if (selectedSince is not null)
                {
                    Step($"Selection moved off '{resultName}'; reselecting it");
                    FindExactResult(resultName).Click();
                }

                selectedSince = null;
            }

            Thread.Sleep(250);
        }

        var target = FindExactResult(resultName);
        Assert.IsTrue(
            IsResultSelected(resultName) && selectedSince is not null && DateTime.UtcNow - selectedSince >= settle,
            $"Result '{resultName}' did not stay selected before double-clicking it.");
        target.DoubleClick();
    }

    private bool IsResultSelected(string resultName)
    {
        try
        {
            return FindExactResult(resultName).Selected;
        }
        catch (AssertFailedException)
        {
            // The result was re-created by a list update between the lookup and the property read.
            return false;
        }
    }

    /// <summary>
    /// Find the first result whose name is exactly <paramref name="resultName"/>. Name lookups
    /// substring-match, so a plain "Downloads" query would also match "Downloads.lnk" or
    /// "Public Downloads".
    /// </summary>
    protected NavigationViewItem FindExactResult(string resultName, int timeoutMS = 5000)
    {
        NavigationViewItem? match = null;
        CommandPaletteSession.WaitFor(
            () =>
            {
                match = CommandPaletteSession
                    .FindAll<NavigationViewItem>(By.Name(resultName), timeoutMS: 0)
                    .FirstOrDefault(e => string.Equals(e.Name, resultName, StringComparison.OrdinalIgnoreCase));
                return match is not null;
            },
            timeoutMS);
        Assert.IsNotNull(match, $"No result named exactly '{resultName}' was found.");
        return match;
    }

    protected void SetFilesExtensionSearchBox(string text) => SetSearchBox(text);

    protected void SetCalculatorExtensionSearchBox(string text) => SetSearchBox(text);

    protected void SetTimeAndDateExtensionSearchBox(string text) => SetSearchBox(text);

    protected void OpenSettingsWindow()
    {
        Step("Opening Command Palette settings");
        CommandPaletteSession.Find<Button>(By.AccessibilityId("SettingsIconButton")).Click();
    }

    protected void NavigateToDockSettings()
    {
        Step("Navigating to Dock settings");
        CommandPaletteSession.Find<NavigationViewItem>("Dock (Preview)").Click();
    }

    protected ToggleSwitch FindDockAutoHideToggle() =>
        CommandPaletteSession.Find<ToggleSwitch>(By.AccessibilityId("CmdPal_DockSettingsPage_AutoHide"));

    protected void OpenContextMenu()
    {
        Step("Opening the selected result's context menu");
        CommandPaletteSession.Find<Button>(By.AccessibilityId("MoreContextMenuButton")).Click();
    }

    protected void Step(string message) =>
        TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] {message}");
}
