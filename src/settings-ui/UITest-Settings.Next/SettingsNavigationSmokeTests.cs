// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Reflection;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Settings.UITests.Next;

/// <summary>
/// Smoke test that drives the Settings shell via winappcli and asserts that clicking every
/// <c>NavigationViewItem</c> leaves the process alive.
/// </summary>
/// <remarks>
/// <para>
/// Inspired by <see href="https://github.com/microsoft/PowerToys/pull/48414"/>. Uses our
/// <see cref="UITestAutomation.Next"/> harness instead of the PR's bare wrapper so the same
/// surface (Find/Click/By/Element) works across all module tests.
/// </para>
/// <para>
/// Settings is launched directly (not via <c>PowerToys.exe</c>) so this test exercises just
/// the shell navigation path and doesn't depend on the runner's tray/elevation/module startup.
/// One <c>[TestMethod]</c> per nav item via <c>[DynamicData]</c> gives a discrete pass/fail
/// per item in Test Explorer / pipeline reports \u2014 if <c>FancyZonesNavItem</c> regresses, the
/// report names it.
/// </para>
/// <para>
/// Selectors are AutomationIds straight from
/// <c>src/settings-ui/Settings.UI/SettingsXAML/Views/ShellPage.xaml</c>; they don't change with
/// the user's MUI language so the test stays localization-independent. Parent groups
/// (<c>SystemToolsNavItem</c>, <c>WindowingAndLayoutsNavItem</c>, <c>InputOutputNavItem</c>,
/// <c>FileManagementNavItem</c>, <c>AdvancedNavItem</c>) have <c>SelectsOnInvoked="False"</c>
/// and only expand on invoke; our <see cref="Element.Click"/> tries InvokePattern \u2192
/// TogglePattern \u2192 SelectionItemPattern \u2192 ExpandCollapsePattern in order so the same
/// call works for both navigation-y leaves and expand-y groups.
/// </para>
/// </remarks>
[TestClass]
public sealed class SettingsNavigationSmokeTests
{
    // (ParentGroupSlug | null, NavItemSlug). Mirrors the live hierarchy in ShellPage.xaml.
    // Footer items (OOBE/WhatIsNew/Feedback/Close) are intentionally excluded \u2014 those use
    // Tapped handlers that open dialogs / external pages and aren't part of the in-shell
    // navigation surface we're guarding against FailFast.
    private static readonly NavigationCase[] NavigationItems = new[]
    {
        // Top-level
        new NavigationCase(null, "DashboardNavItem"),
        new NavigationCase(null, "GeneralNavItem"),

        // System tools
        new NavigationCase("SystemToolsNavItem", "AdvancedPasteNavItem"),
        new NavigationCase("SystemToolsNavItem", "AwakeNavItem"),
        new NavigationCase("SystemToolsNavItem", "CmdPalNavItem"),
        new NavigationCase("SystemToolsNavItem", "ColorPickerNavItem"),
        new NavigationCase("SystemToolsNavItem", "LightSwitchNavItem"),
        new NavigationCase("SystemToolsNavItem", "PowerLauncherNavItem"),
        new NavigationCase("SystemToolsNavItem", "ScreenRulerNavItem"),
        new NavigationCase("SystemToolsNavItem", "ShortcutGuideNavItem"),
        new NavigationCase("SystemToolsNavItem", "TextExtractorNavItem"),
        new NavigationCase("SystemToolsNavItem", "ZoomItNavItem"),

        // Windowing and layouts
        new NavigationCase("WindowingAndLayoutsNavItem", "AlwaysOnTopNavItem"),
        new NavigationCase("WindowingAndLayoutsNavItem", "CropAndLockNavItem"),
        new NavigationCase("WindowingAndLayoutsNavItem", "FancyZonesNavItem"),
        new NavigationCase("WindowingAndLayoutsNavItem", "GrabAndMoveNavItem"),
        new NavigationCase("WindowingAndLayoutsNavItem", "WorkspacesNavItem"),

        // Input / Output
        new NavigationCase("InputOutputNavItem", "KeyboardManagerNavItem"),
        new NavigationCase("InputOutputNavItem", "MouseUtilitiesNavItem"),
        new NavigationCase("InputOutputNavItem", "MouseWithoutBordersNavItem"),
        new NavigationCase("InputOutputNavItem", "PowerDisplayNavItem"),
        new NavigationCase("InputOutputNavItem", "QuickAccentNavItem"),

        // File management
        new NavigationCase("FileManagementNavItem", "PowerPreviewNavItem"),
        new NavigationCase("FileManagementNavItem", "FileLocksmithNavItem"),
        new NavigationCase("FileManagementNavItem", "ImageResizerNavItem"),
        new NavigationCase("FileManagementNavItem", "NewPlusNavItem"),
        new NavigationCase("FileManagementNavItem", "PeekNavItem"),
        new NavigationCase("FileManagementNavItem", "PowerRenameNavItem"),

        // Advanced
        new NavigationCase("AdvancedNavItem", "CmdNotFoundNavItem"),
        new NavigationCase("AdvancedNavItem", "EnvironmentVariablesNavItem"),
        new NavigationCase("AdvancedNavItem", "HostsNavItem"),
        new NavigationCase("AdvancedNavItem", "RegistryPreviewNavItem"),
    };

    private const string SettingsProcessName = "PowerToys.Settings";
    private const string SettingsExeName = "PowerToys.Settings.exe";
    private const string SettingsSubDirectory = "WinUI3Apps";

    // Parent groups are only expanded on first encounter \u2014 mirrors the PR's _expandedGroups set.
    private static readonly HashSet<string> ExpandedGroups = new(StringComparer.Ordinal);

    private static Process? settingsProcess;
    private static Session? session;

    public TestContext TestContext { get; set; } = null!;

    [ClassInitialize]
    public static void ClassInit(TestContext _)
    {
        Assert.IsTrue(WinappCli.IsAvailable(), WinappCli.InstallHint);

        settingsProcess = LaunchSettings();

        // Process-scope session: PR #48414 pattern. Single window, no need to pin a HWND \u2014 the
        // CLI re-resolves -a on every call so page swaps don't invalidate the binding.
        session = Session.FromProcess(SettingsProcessName, PowerToysModule.PowerToysSettings, timeoutMS: 20_000);

        // Confirm the shell finished loading by waiting for an item that's always present.
        Assert.IsTrue(
            session!.Find<NavigationViewItem>(By.AccessibilityId("DashboardNavItem"), timeoutMS: 15_000) is not null,
            "Settings shell did not present DashboardNavItem within 15s.");
    }

    [ClassCleanup]
    public static void ClassTeardown()
    {
        WindowControl.TryCloseByApp(SettingsProcessName);

        if (settingsProcess is not null)
        {
            try
            {
                if (!settingsProcess.HasExited)
                {
                    settingsProcess.Kill(entireProcessTree: true);
                    settingsProcess.WaitForExit(5_000);
                }
            }
            catch (InvalidOperationException)
            {
                // Process already gone.
            }
            finally
            {
                settingsProcess.Dispose();
                settingsProcess = null;
            }
        }

        session = null;
        ExpandedGroups.Clear();
    }

    public static IEnumerable<object[]> NavigationCases()
    {
        foreach (var c in NavigationItems)
        {
            yield return new object[] { c.ParentGroupSlug ?? string.Empty, c.NavItemSlug };
        }
    }

    public static string GetNavCaseDisplayName(MethodInfo _, object[] data)
    {
        var parent = (string)data[0];
        var item = (string)data[1];
        return string.IsNullOrEmpty(parent) ? item : $"{parent} -> {item}";
    }

    [TestMethod]
    [TestCategory("Settings")]
    [TestCategory("winappcli-POC")]
    [DynamicData(nameof(NavigationCases), DynamicDataDisplayName = nameof(GetNavCaseDisplayName))]
    public void NavigationItem_NavigatesWithoutCrashing(string parentGroupSlug, string navItemSlug)
    {
        Assert.IsNotNull(session, "Session was not initialized in ClassInit.");
        Assert.IsNotNull(settingsProcess, "Settings process was not launched in ClassInit.");

        if (!string.IsNullOrEmpty(parentGroupSlug) && ExpandedGroups.Add(parentGroupSlug))
        {
            session!.Find<NavigationViewItem>(By.AccessibilityId(parentGroupSlug)).Click();
        }

        // Child item is only in the visual tree once its parent is expanded; the harness's
        // Find polls for up to timeoutMS so the expand animation doesn't race us.
        session!.Find<NavigationViewItem>(By.AccessibilityId(navItemSlug), timeoutMS: 5_000).Click();

        // Brief settle so any unhandled exception in the page constructor or navigation handler
        // has time to land in RoFailFast.
        Thread.Sleep(250);

        settingsProcess!.Refresh();
        Assert.IsFalse(
            settingsProcess.HasExited,
            $"PowerToys.Settings.exe exited after invoking '{navItemSlug}' (exit code {settingsProcess.ExitCode}). " +
            "Likely a navigation FailFast regression \u2014 see ShellViewModel.Frame_NavigationFailed.");
    }

    private static Process LaunchSettings()
    {
        var exe = LocateSettingsExe()
            ?? throw new FileNotFoundException(
                $"Could not find {SettingsExeName} in any installed or dev-build location.");

        var psi = new ProcessStartInfo
        {
            FileName = exe,
            WorkingDirectory = Path.GetDirectoryName(exe)!,
            UseShellExecute = false,
        };

        var p = Process.Start(psi)
            ?? throw new InvalidOperationException($"Process.Start returned null for {exe}");

        WaitForMainWindow(p, TimeSpan.FromSeconds(30));
        return p;
    }

    private static string? LocateSettingsExe()
    {
        foreach (var root in CandidateInstallRoots())
        {
            var path = Path.Combine(root, SettingsSubDirectory, SettingsExeName);
            if (File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }

    private static IEnumerable<string> CandidateInstallRoots()
    {
        // Installed PowerToys (preferred in pipeline runs).
        foreach (var path in new[]
        {
            @"C:\Program Files\PowerToys",
            @"C:\Program Files (x86)\PowerToys",
            Environment.ExpandEnvironmentVariables(@"%LocalAppData%\PowerToys"),
        })
        {
            if (Directory.Exists(path))
            {
                yield return path;
            }
        }

        // Dev build sitting near the test assembly. The csproj's OutputPath puts us at
        // <repo>\<plat>\<cfg>\... and PowerToys.Settings.exe lands at <repo>\<plat>\<cfg>\WinUI3Apps\...
        var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        if (assemblyDir is not null)
        {
            var probe = new DirectoryInfo(assemblyDir);
            for (int i = 0; i < 6 && probe is not null; i++, probe = probe.Parent)
            {
                if (Directory.Exists(Path.Combine(probe.FullName, SettingsSubDirectory)))
                {
                    yield return probe.FullName;
                }
            }
        }
    }

    private static void WaitForMainWindow(Process process, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            process.Refresh();
            if (process.HasExited)
            {
                throw new InvalidOperationException(
                    $"{SettingsExeName} exited during startup with code {process.ExitCode}.");
            }

            if (process.MainWindowHandle != IntPtr.Zero)
            {
                Thread.Sleep(750);
                return;
            }

            Thread.Sleep(100);
        }

        throw new TimeoutException($"{SettingsExeName} did not produce a main window within {timeout.TotalSeconds}s.");
    }

    private readonly record struct NavigationCase(string? ParentGroupSlug, string NavItemSlug);
}
