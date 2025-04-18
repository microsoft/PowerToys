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
