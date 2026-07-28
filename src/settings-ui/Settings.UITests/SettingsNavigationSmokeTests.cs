// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Reflection;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Settings.UITests;

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
/// Inherits <see cref="UITestBase"/> with <see cref="UITestBase.ReuseScopeAcrossTests"/> on, so a
/// single Settings window is reused across every nav-item case (one launch per class, not per test)
/// while still getting the framework's unified failure-media capture for free — no test-local
/// screenshot code. One method per nav item via <c>[DynamicData]</c> gives a discrete pass/fail per
/// item in Test Explorer / pipeline reports — if <c>FancyZonesNavItem</c> regresses, the report names it.
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
public sealed class SettingsNavigationSmokeTests : UITestBase
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

    private const string ScopeProcessName = "PowerToys.Settings";
    private const PowerToysModule Scope = PowerToysModule.PowerToysSettings;

    public SettingsNavigationSmokeTests()
        : base(Scope)
    {
    }

    // Reuse one Settings window across all nav-item cases (no per-test relaunch); the framework
    // still captures failure media per test and stops Settings once the class finishes.
    protected override bool ReuseScopeAcrossTests => true;

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
        // The Settings window is shared across the class, so a parent group may already be expanded
        // from a previous case. Only expand it when the child isn't already in the tree — clicking
        // an already-expanded group would collapse it.
        if (!string.IsNullOrEmpty(parentGroupSlug) && !Session.Has(By.AccessibilityId(navItemSlug), 500))
        {
            Find<NavigationViewItem>(By.AccessibilityId(parentGroupSlug)).Click();
        }

        // Child item is only in the visual tree once its parent is expanded; Find polls for up to
        // timeoutMS so the expand animation doesn't race us.
        Find<NavigationViewItem>(By.AccessibilityId(navItemSlug), timeoutMS: 5_000).Click();

        // Brief settle so any unhandled exception in the page constructor or navigation handler
        // has time to land in RoFailFast.
        Thread.Sleep(250);

        // Check by process name, not by launcher PID. Settings is single-instance: the EXE the
        // framework started often exits cleanly after handing off to an existing instance, so the
        // actual window may be owned by a different PID than the one we launched.
        Assert.IsTrue(
            SessionHelper.IsRunning(Scope),
            $"No {ScopeProcessName} process remains after invoking '{navItemSlug}'. " +
            "Likely a navigation FailFast regression \u2014 see ShellViewModel.Frame_NavigationFailed.");
    }

    private readonly record struct NavigationCase(string? ParentGroupSlug, string NavItemSlug);
}

