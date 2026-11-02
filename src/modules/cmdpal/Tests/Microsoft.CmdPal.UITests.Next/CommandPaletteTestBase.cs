// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Globalization;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UITests;

public class CommandPaletteTestBase : UITestBase
{
    private Session? commandPaletteSession;
    private CmdPalDiagnosticTrace? diagnosticTrace;

    public CommandPaletteTestBase()
        : base(PowerToysModule.CommandPalette)
    {
    }

    protected override IReadOnlyList<string> StaleProcessNames =>
        [.. base.StaleProcessNames, "Microsoft.CmdPal.UI"];

    protected Session CommandPaletteSession =>
        commandPaletteSession ??= Session.FromProcess(
            Session.ProcessId.ToString(CultureInfo.InvariantCulture),
            PowerToysModule.CommandPalette,
            timeoutMS: 10_000);

    protected IDisposable BeginNavigationDiagnostics() =>
        diagnosticTrace = new CmdPalDiagnosticTrace(TestContext, Session);

    protected void SetSearchBox(string text)
    {
        EnsureCommandPaletteForeground();
        Step($"Setting Command Palette search to '{text}'");
        var searchBox = CommandPaletteSession.Find<TextBox>(By.AccessibilityId("MainSearchBox"), timeoutMS: 45_000);
        diagnosticTrace?.Record("MainSearchBox-found", new { Query = text, CommandPaletteSession.TargetFlag, CommandPaletteSession.TargetValue });
        searchBox.SetText(text);
        diagnosticTrace?.Record("MainSearchBox-value-set", new { Query = text });
        Assert.IsTrue(searchBox.WaitForValue(text, timeoutMS: 10_000), $"The search box did not update to '{text}'.");
        diagnosticTrace?.Record("MainSearchBox-value-confirmed", new { Query = text });
    }

    /// <summary>
    /// Command Palette dismisses itself whenever its window is deactivated. On a slow (1 vCPU) machine
    /// the cold-started window can lose activation before the test's first interaction, leaving it
    /// cloaked. Activating the module redirects to the running instance, which summons the window.
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

            var mainWindow = WindowsFinder.ListAll()
                .FirstOrDefault(window => window.ProcessId == Session.ProcessId && window.Title == "Command Palette");
            Step($"Foreground={WindowControl.GetForegroundWindowInfo()}; main={mainWindow}; initial HWND={Session.WindowHandle}");
            if (mainWindow is not null &&
                !WindowHelper.IsWindowCloaked(new IntPtr(mainWindow.Hwnd)) &&
                WindowControl.WaitForForeground(
                    new IntPtr(mainWindow.Hwnd), timeoutMS: 5_000, requiredConsecutiveMatches: 2))
            {
                return;
            }

            var foreground = WindowControl.GetForegroundWindowInfo();
            if (foreground.ProcessName == "SearchHost" ||
                (foreground.ProcessName == "ShellExperienceHost" && foreground.Title == "New notification"))
            {
                Step($"Dismissing the foreground shell surface '{foreground.Title}' before re-summoning Command Palette");
                KeyboardHelper.SendKeys(Key.Esc);
            }

            Step($"Command Palette is not in the foreground (foreground: '{foreground.ProcessName}' '{foreground.Title}'); re-summoning");
            SessionHelper.LaunchScope(PowerToysModule.CommandPalette);

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
    /// Resolve the exact result after layout and selection settle. Retry only when winapp confirms
    /// that it sent no double-click because the target was still moving.
    /// </summary>
    protected void DoubleClickResult(Element item) => DoubleClickResult(item.Name);

    protected void DoubleClickResult(string resultName)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var target = SelectResult(resultName);
            try
            {
                target.DoubleClick();
                diagnosticTrace?.Record("double-click-sent", new { Result = resultName });
                return;
            }
            catch (AssertFailedException ex) when (
                attempt < 2 && ex.Message.Contains("moving/resizing", StringComparison.Ordinal))
            {
                Step($"Double-click was not sent because '{resultName}' moved; waiting for its layout again");
            }
        }
    }

    protected Element SelectResult(string resultName, int timeoutMS = 45_000)
    {
        EnsureCommandPaletteForeground();
        Step($"Waiting for stable layout and selection of '{resultName}'");
        (int X, int Y, int Width, int Height)? previousBounds = null;
        var boundsStable = false;
        var selection = WaitHelper.WaitForStable(
            observe: () => TryFindExactResult(resultName),
            isMatch: item =>
            {
                boundsStable = false;
                if (item is null)
                {
                    previousBounds = null;
                    return false;
                }

                var bounds = (item.X, item.Y, item.Width, item.Height);
                boundsStable = previousBounds == bounds && item.Width > 0 && item.Height > 0 && item.Displayed;
                previousBounds = bounds;
                return boundsStable && item.Selected;
            },
            timeoutMS: timeoutMS,
            requiredConsecutiveMatches: 2,
            pollIntervalMS: 200,
            recover: item =>
            {
                if (boundsStable && item is not null)
                {
                    item.Click(msPostAction: 0);
                }
            },
            shouldRetryException: exception => exception is AssertFailedException &&
                exception.Message.Contains("stale_element", StringComparison.OrdinalIgnoreCase));
        Assert.IsTrue(selection.Succeeded, $"Result '{resultName}' did not settle with stable selection and bounds.");
        Assert.IsNotNull(selection.LastObservation);
        return selection.LastObservation;
    }

    /// <summary>
    /// Find the first result whose name is exactly <paramref name="resultName"/>. Name lookups
    /// substring-match, so a plain "Downloads" query would also match "Downloads.lnk" or
    /// "Public Downloads".
    /// </summary>
    protected Element FindExactResult(string resultName, int timeoutMS = 5000)
    {
        Element? match = null;
        CommandPaletteSession.WaitFor(
            () =>
            {
                match = TryFindExactResult(resultName);
                return match is not null;
            },
            timeoutMS);
        if (match is null)
        {
            Step($"Missing exact result '{resultName}'; foreground={WindowControl.GetForegroundWindowInfo()}");
            TestContext.WriteLine(CommandPaletteSession.Inspect(depth: 8).GetRawText());
        }

        Assert.IsNotNull(match, $"No result named exactly '{resultName}' was found.");
        return match;
    }

    private Element? TryFindExactResult(string resultName) =>
        CommandPaletteSession.FindAll<Element>(By.Name(resultName), timeoutMS: 0)
            .FirstOrDefault(item => item.ControlType == "ListItem" &&
                string.Equals(item.Name, resultName, StringComparison.OrdinalIgnoreCase));

    protected void SetFilesExtensionSearchBox(string text) => SetSearchBox(text);

    protected void SetCalculatorExtensionSearchBox(string text) => SetSearchBox(text);

    protected void SetTimeAndDateExtensionSearchBox(string text) => SetSearchBox(text);

    protected void OpenExtension(string query, string title)
    {
        SetSearchBox(query);
        Step($"Opening the '{title}' extension");
        InvokeResultWithEnter(title);
        Step($"Waiting for the '{title}' destination page's Back control");
        Assert.IsNotNull(CommandPaletteSession.Find<Button>("Back", timeoutMS: 45_000));
        diagnosticTrace?.Record("destination-Back-found", new { Extension = title });
    }

    protected void InvokeResultWithEnter(string title)
    {
        Step($"Invoking '{title}' with Enter");
        SelectResult(title);
        EnsureCommandPaletteForeground();
        diagnosticTrace?.Record("Enter-sending", new { Result = title });
        KeyboardHelper.SendChord(Key.Enter);
        diagnosticTrace?.Record("Enter-sent", new { Result = title });
    }

    protected void AssertWindowsSettingsOpened()
    {
        // Windows 10 can replace its splash HWND before the Settings content becomes available.
        var ready = WaitHelper.WaitForStable(
            observe: () => WindowsFinder.WaitForWindow(
                window => window.Title == "Settings" &&
                    window.ProcessName is "SystemSettings" or "ApplicationFrameHost",
                timeoutMS: 1_000),
            isMatch: settings => settings is not null &&
                settings.FindAll<TextBox>(By.Name("Find a setting"), timeoutMS: 0).Count > 0,
            timeoutMS: 60_000,
            requiredConsecutiveMatches: 2,
            pollIntervalMS: 200,
            shouldRetryException: exception => exception is AssertFailedException &&
                exception.Message.Contains("Window HWND", StringComparison.Ordinal) &&
                exception.Message.Contains("AppNotFoundException", StringComparison.Ordinal));
        Assert.IsTrue(
            ready.Succeeded,
            $"The command did not open a fully loaded Windows Settings application. {ready.LastException?.Message}");
    }

    protected static HashSet<long> SnapshotWindowsSettings() =>
        WindowsFinder.ListAll().Where(IsWindowsSettingsWindow).Select(window => window.Hwnd).ToHashSet();

    protected static void CloseCreatedWindowsSettings(IReadOnlySet<long> windowsBefore)
    {
        foreach (var window in WindowsFinder.ListAll().Where(IsWindowsSettingsWindow))
        {
            if (!windowsBefore.Contains(window.Hwnd))
            {
                WindowControl.TryCloseWindow(window.Hwnd);
            }
        }
    }

    private static bool IsWindowsSettingsWindow(WindowsFinder.WindowInfo window) =>
        window.Title == "Settings" && window.ProcessName is "SystemSettings" or "ApplicationFrameHost";

    protected void OpenSettingsWindow()
    {
        Step("Opening Command Palette settings");
        EnsureCommandPaletteForeground();
        Assert.IsNotNull(CommandPaletteSession.Find<TextBox>(By.AccessibilityId("MainSearchBox"), timeoutMS: 45_000));
        for (var depth = 0; depth < 5 &&
             !CommandPaletteSession.Has(By.AccessibilityId("SettingsIconButton"), timeoutMS: 500); depth++)
        {
            CommandPaletteSession.Find<Button>("Back").Invoke(msPostAction: 0);
        }

        CommandPaletteSession.Find<Button>(By.AccessibilityId("SettingsIconButton")).Invoke();
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

    protected void Step(string message)
    {
        diagnosticTrace?.Record(message);
        TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] {message}");
    }
}
