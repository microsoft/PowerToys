// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.PowerToys.UITest;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Settings.UITests
{
    /// <summary>
    /// End-to-end smoke tests that exercise every Settings shell NavigationView item
    /// and assert the Settings process stays alive throughout.
    ///
    /// <para>
    /// These tests are the runtime counterpart to the unit tests on
    /// <c>ShellViewModel.HandleNavigationFailure</c>. The unit tests prove the helper
    /// does not throw for null inputs; these UI tests prove that any module page that
    /// would fail to construct (and therefore raise <c>Frame.NavigationFailed</c>) does
    /// not crash the Settings process - i.e. <c>Frame_NavigationFailed</c> does not
    /// re-throw and trigger a WinUI fail-fast.
    /// </para>
    /// </summary>
    [TestClass]
    public class NavigationSmokeTests : UITestBase
    {
        private const string SettingsWindowTitle = "Settings";

        // AutomationId of each NavigationViewItem in the shell. Using AutomationId
        // (rather than Content) makes the test localization-independent.
        // Parent group items use SelectsOnInvoked="False" and only expand a submenu
        // when clicked - they must be expanded before their children become visible.
        private static readonly (string? ParentId, string ItemId)[] NavigationItems = new (string?, string)[]
        {
            // Top-level (always visible)
            (null, "DashboardNavItem"),
            (null, "GeneralNavItem"),

            // System tools
            ("SystemToolsNavItem", "AdvancedPasteNavItem"),
            ("SystemToolsNavItem", "AwakeNavItem"),
            ("SystemToolsNavItem", "CmdPalNavItem"),
            ("SystemToolsNavItem", "ColorPickerNavItem"),
            ("SystemToolsNavItem", "LightSwitchNavItem"),
            ("SystemToolsNavItem", "PowerLauncherNavItem"),
            ("SystemToolsNavItem", "MeasureToolNavItem"),
            ("SystemToolsNavItem", "ShortcutGuideNavItem"),
            ("SystemToolsNavItem", "TextExtractorNavItem"),
            ("SystemToolsNavItem", "ZoomItNavItem"),

            // Windowing & layouts
            ("WindowingAndLayoutsNavItem", "AlwaysOnTopNavItem"),
            ("WindowingAndLayoutsNavItem", "CropAndLockNavItem"),
            ("WindowingAndLayoutsNavItem", "FancyZonesNavItem"),
            ("WindowingAndLayoutsNavItem", "GrabAndMoveNavItem"),
            ("WindowingAndLayoutsNavItem", "WorkspacesNavItem"),

            // Input / Output
            ("InputOutputNavItem", "KeyboardManagerNavItem"),
            ("InputOutputNavItem", "MouseUtilitiesNavItem"),
            ("InputOutputNavItem", "MouseWithoutBordersNavItem"),
            ("InputOutputNavItem", "PowerDisplayNavItem"),
            ("InputOutputNavItem", "QuickAccentNavItem"),

            // File management
            ("FileManagementNavItem", "PowerPreviewNavItem"),
            ("FileManagementNavItem", "FileLocksmithNavItem"),
            ("FileManagementNavItem", "ImageResizerNavItem"),
            ("FileManagementNavItem", "NewPlusNavItem"),
            ("FileManagementNavItem", "PeekNavItem"),
            ("FileManagementNavItem", "PowerRenameNavItem"),

            // Advanced
            ("AdvancedNavItem", "CmdNotFoundNavItem"),
            ("AdvancedNavItem", "EnvironmentVariablesNavItem"),
            ("AdvancedNavItem", "HostsNavItem"),
            ("AdvancedNavItem", "RegistryPreviewNavItem"),
        };

        public NavigationSmokeTests()
            : base(PowerToysModule.PowerToysSettings, size: WindowSize.Large)
        {
        }

        [TestMethod("PowerToys.Settings.Navigation.AllItemsNavigateWithoutCrashing")]
        [TestCategory("Settings Navigation Smoke")]
        public void AllNavigationViewItems_NavigateWithoutCrashing()
        {
            var expandedGroups = new HashSet<string>();

            foreach (var (parentId, itemId) in NavigationItems)
            {
                if (parentId != null && expandedGroups.Add(parentId))
                {
                    var parent = this.Find<NavigationViewItem>(By.AccessibilityId(parentId));
                    Assert.IsNotNull(parent, $"Could not find parent NavigationViewItem '{parentId}'.");
                    parent.Click();
                    Task.Delay(300).Wait();
                }

                var item = this.Find<NavigationViewItem>(By.AccessibilityId(itemId));
                Assert.IsNotNull(item, $"Could not find NavigationViewItem '{itemId}'.");
                item.Click();
                Task.Delay(500).Wait();

                // If a page constructor or navigation handler had thrown back into the
                // WinUI dispatcher (the regression behind ShellViewModel.Frame_NavigationFailed),
                // the Settings process would have been FailFasted by the runtime and this
                // check would fail.
                Assert.IsTrue(
                    this.IsWindowOpen(SettingsWindowTitle),
                    $"Settings window terminated after navigating to '{itemId}'. This indicates a regression in Frame_NavigationFailed re-throwing or in a page constructor.");
            }
        }
    }
}
