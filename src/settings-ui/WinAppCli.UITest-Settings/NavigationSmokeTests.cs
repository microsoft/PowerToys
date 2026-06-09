// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Settings.UITests.WinAppCli
{
    /// <summary>
    /// Smoke test that drives the Settings shell via the <c>winapp</c> CLI (UIA-backed,
    /// no WinAppDriver / Appium dependency) and asserts that clicking every
    /// <c>NavigationViewItem</c> leaves the process alive.
    ///
    /// The companion unit tests in <c>Settings.UI.UnitTests\ViewModelTests\ShellViewModelTests.cs</c>
    /// cover the pure-logic half of the FailFast regression
    /// (<c>HandleNavigationFailure</c>, <c>GetPageDisplayName</c>). They cannot exercise the
    /// "throw from <c>Frame_NavigationFailed</c> -> WinUI marshalling -> RoFailFast" path
    /// because <c>NavigationFailedEventArgs</c> is a sealed WinRT type and cannot be
    /// constructed from a unit test. This smoke test is the runtime counterpart that catches
    /// any regression where a module page constructor throws, or where the navigation handler
    /// re-introduces an unhandled exception.
    /// </summary>
    [TestClass]
    public sealed class NavigationSmokeTests
    {
        // (ParentGroupSlug | null, NavItemSlug). Parent groups have SelectsOnInvoked="False"
        // in ShellPage.xaml and only expand on invoke; child items navigate. Selectors are
        // AutomationIds taken straight from ShellPage.xaml so the test stays
        // localization-independent.
        private static readonly NavigationCase[] NavigationItems = new[]
        {
            // Top-level
            new NavigationCase(null, "DashboardNavItem"),
            new NavigationCase(null, "GeneralNavItem"),

            // System tools
            new NavigationCase("SystemToolsNavItem", "AdvancedPasteNavItem"),
            new NavigationCase("SystemToolsNavItem", "CmdNotFoundNavItem"),
            new NavigationCase("SystemToolsNavItem", "CommandPaletteNavItem"),
            new NavigationCase("SystemToolsNavItem", "CropAndLockNavItem"),
            new NavigationCase("SystemToolsNavItem", "EnvironmentVariablesNavItem"),
            new NavigationCase("SystemToolsNavItem", "HostsFileEditorNavItem"),
            new NavigationCase("SystemToolsNavItem", "RegistryPreviewNavItem"),
            new NavigationCase("SystemToolsNavItem", "ZoomItNavItem"),

            // Windowing and layouts
            new NavigationCase("WindowingAndLayoutsNavItem", "AlwaysOnTopNavItem"),
            new NavigationCase("WindowingAndLayoutsNavItem", "FancyZonesNavItem"),
            new NavigationCase("WindowingAndLayoutsNavItem", "WindowWalkerNavItem"),
            new NavigationCase("WindowingAndLayoutsNavItem", "WorkspacesNavItem"),

            // Input / Output
            new NavigationCase("InputOutputNavItem", "ColorPickerNavItem"),
            new NavigationCase("InputOutputNavItem", "KeyboardManagerNavItem"),
            new NavigationCase("InputOutputNavItem", "MouseUtilsNavItem"),
            new NavigationCase("InputOutputNavItem", "MouseWithoutBordersNavItem"),
            new NavigationCase("InputOutputNavItem", "PowerOcrNavItem"),
            new NavigationCase("InputOutputNavItem", "QuickAccentNavItem"),
            new NavigationCase("InputOutputNavItem", "ScreenRulerNavItem"),
            new NavigationCase("InputOutputNavItem", "ShortcutGuideNavItem"),
            new NavigationCase("InputOutputNavItem", "TextExtractorNavItem"),

            // File management
            new NavigationCase("FileManagementNavItem", "FileExplorerPreviewNavItem"),
            new NavigationCase("FileManagementNavItem", "FileLocksmithNavItem"),
            new NavigationCase("FileManagementNavItem", "ImageResizerNavItem"),
            new NavigationCase("FileManagementNavItem", "NewPlusNavItem"),
            new NavigationCase("FileManagementNavItem", "PowerRenameNavItem"),

            // Advanced
            new NavigationCase("AdvancedNavItem", "AwakeNavItem"),
            new NavigationCase("AdvancedNavItem", "LightSwitchNavItem"),
        };

        private static SettingsHost? _host;
        private static readonly HashSet<string> _expandedGroups = new(StringComparer.Ordinal);

        [ClassInitialize]
        public static void ClassSetup(TestContext _)
        {
            Assert.IsTrue(WinAppCli.IsAvailable(), "winapp CLI is not on PATH. Install with 'winget install Microsoft.winappcli'.");

            _host = new SettingsHost();
            _host.Launch();

            // Confirm the shell finished loading by waiting for an item that's always present.
            var dashboardVisible = WinAppCli.WaitFor(_host.Pid, "DashboardNavItem", timeoutMs: 15_000);
            Assert.IsTrue(dashboardVisible.Succeeded, $"Settings shell did not present DashboardNavItem within 15s. {dashboardVisible.DescribeFailure()}");
        }

        [ClassCleanup]
        public static void ClassTeardown()
        {
            _host?.Dispose();
            _host = null;
        }

        public static IEnumerable<object[]> NavigationCases()
        {
            foreach (var c in NavigationItems)
            {
                yield return new object[] { c.ParentGroupSlug ?? string.Empty, c.NavItemSlug };
            }
        }

        public static string GetNavCaseDisplayName(System.Reflection.MethodInfo _, object[] data)
        {
            var parent = (string)data[0];
            var item = (string)data[1];
            return string.IsNullOrEmpty(parent) ? item : $"{parent} -> {item}";
        }

        [TestMethod]
        [DynamicData(nameof(NavigationCases), DynamicDataDisplayName = nameof(GetNavCaseDisplayName))]
        public void NavigationItem_NavigatesWithoutCrashing(string parentGroupSlug, string navItemSlug)
        {
            Assert.IsNotNull(_host, "SettingsHost was not initialized.");

            if (!string.IsNullOrEmpty(parentGroupSlug) && _expandedGroups.Add(parentGroupSlug))
            {
                var expand = WinAppCli.Invoke(_host.Pid, parentGroupSlug);
                Assert.IsTrue(expand.Succeeded, $"Failed to expand parent group '{parentGroupSlug}'. {expand.DescribeFailure()}");
            }

            // The child item is only in the visual tree once its parent is expanded. Wait briefly
            // so we don't race the animation.
            var waitForItem = WinAppCli.WaitFor(_host.Pid, navItemSlug, timeoutMs: 5_000);
            Assert.IsTrue(waitForItem.Succeeded, $"Could not find '{navItemSlug}'. {waitForItem.DescribeFailure()}");

            var invokeItem = WinAppCli.Invoke(_host.Pid, navItemSlug);
            Assert.IsTrue(invokeItem.Succeeded, $"Failed to invoke '{navItemSlug}'. {invokeItem.DescribeFailure()}");

            // Give the navigation a moment to land before asserting the process is alive — if a
            // page constructor throws and the (now-fixed) handler ever regresses, the FailFast
            // will land in this window.
            Thread.Sleep(250);

            using var alive = Process.GetProcessById(_host.Pid);
            alive.Refresh();
            Assert.IsFalse(alive.HasExited, $"PowerToys.Settings.exe exited after invoking '{navItemSlug}' (exit code {alive.ExitCode}). Likely a navigation FailFast regression.");
        }

        private readonly record struct NavigationCase(string? ParentGroupSlug, string NavItemSlug);
    }
}
