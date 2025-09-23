// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.PowerToys.UITest;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium.Appium.Windows;

namespace MouseUtils.UITests
{
    public class MouseUtilsSettings
    {
        // Accessibility ID constants
        public static class AccessibilityIds
        {
            // Mouse Utils module IDs
            public const string FindMyMouse = "MouseUtils_FindMyMouseTestId";
            public const string MouseHighlighter = "MouseUtils_MouseHighlighterTestId";
            public const string MousePointerCrosshairs = "MouseUtils_MousePointerCrosshairsTestId";
            public const string MouseJump = "MouseUtils_MouseJumpTestId";

            // ToggleSwitch IDs
            public const string FindMyMouseToggle = "MouseUtils_FindMyMouseToggleId";
            public const string MouseHighlighterToggle = "MouseUtils_MouseHighlighterToggleId";
            public const string MousePointerCrosshairsToggle = "MouseUtils_MousePointerCrosshairsToggleId";
            public const string MouseJumpToggle = "MouseUtils_MouseJumpToggleId";

            // Find My Mouse UI Element IDs
            public const string FindMyMouseActivationMethod = "MouseUtils_FindMyMouseActivationMethodId";
            public const string FindMyMouseAppearanceBehavior = "MouseUtils_FindMyMouseAppearanceBehaviorId";
            public const string FindMyMouseExcludedApps = "MouseUtils_FindMyMouseExcludedAppsId";
            public const string FindMyMouseBackgroundColor = "MouseUtils_FindMyMouseBackgroundColorId";
            public const string FindMyMouseSpotlightColor = "MouseUtils_FindMyMouseSpotlightColorId";
            public const string FindMyMouseOverlayOpacity = "MouseUtils_FindMyMouseOverlayOpacityId";
            public const string FindMyMouseSpotlightZoom = "MouseUtils_FindMyMouseSpotlightZoomId";
            public const string FindMyMouseSpotlightRadius = "MouseUtils_FindMyMouseSpotlightRadiusId";
            public const string FindMyMouseAnimationDuration = "MouseUtils_FindMyMouseAnimationDurationId";

            // Mouse Highlighter UI Element IDs
            public const string MouseHighlighterActivationShortcut = "MouseUtils_MouseHighlighterActivationShortcutId";
            public const string MouseHighlighterAppearanceBehavior = "MouseUtils_MouseHighlighterAppearanceBehaviorId";

            // Mouse Pointer Crosshairs UI Element IDs
            public const string MousePointerCrosshairsAppearanceBehavior = "MouseUtils_MousePointerCrosshairsAppearanceBehaviorId";

            // Mouse Jump UI Element IDs
            public const string MouseJumpActivationShortcut = "MouseUtils_MouseJumpActivationShortcutId";

            // Navigation IDs
            public const string InputOutputNavItem = "InputOutputNavItem";
            public const string MouseUtilitiesNavItem = "MouseUtilitiesNavItem";
            public const string KeyboardManagerNavItem = "KeyboardManagerNavItem";
        }

        // Mouse Utils Modules
        public enum MouseUtils
        {
            MouseHighlighter,
            FindMyMouse,
            MousePointerCrosshairs,
            MouseJump,
        }

        private static readonly Dictionary<MouseUtils, string> MouseUtilUINameMap = new()
        {
            [MouseUtils.MouseHighlighter] = @"Mouse Highlighter",
            [MouseUtils.FindMyMouse] = @"Find My Mouse",
            [MouseUtils.MousePointerCrosshairs] = @"Mouse Pointer Crosshairs",
            [MouseUtils.MouseJump] = @"Mouse Jump",
        };

        private static readonly Dictionary<MouseUtils, string> MouseUtilUIToggleMap = new()
        {
            [MouseUtils.MouseHighlighter] = @"Enable Mouse Highlighter",
            [MouseUtils.FindMyMouse] = @"Enable Find My Mouse",
            [MouseUtils.MousePointerCrosshairs] = @"Enable Mouse Pointer Crosshairs",
            [MouseUtils.MouseJump] = @"Enable Mouse Jump",
        };

        public static string GetMouseUtilUIName(MouseUtils element)
        {
            return MouseUtilUINameMap[element];
        }

        public static void SetMouseUtilEnabled(Custom? custom, MouseUtils element, bool isEnable = true)
        {
            if (custom != null)
            {
                string toggleName = MouseUtilUIToggleMap[element];
                var toggle = custom.Find<ToggleSwitch>(toggleName);

                toggle.Toggle(isEnable);
            }
            else
            {
                Assert.Fail(element + " custom not found.");
            }
        }
    }
}
