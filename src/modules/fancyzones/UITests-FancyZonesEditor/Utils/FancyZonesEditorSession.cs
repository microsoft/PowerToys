// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using Microsoft.FancyZonesEditor.UITests;
using Microsoft.FancyZonesEditor.UITests.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;
using OpenQA.Selenium.Interactions;
using OpenQA.Selenium.Support.UI;

namespace Microsoft.FancyZonesEditor.UnitTests.Utils
{
    public class FancyZonesEditorSession
    {
        protected const string WindowsApplicationDriverUrl = "http://127.0.0.1:4723";
        private const string FancyZonesEditorName = "PowerToys.FancyZonesEditor";
        private const string FancyZonesEditorPath = @"\..\..\..\" + FancyZonesEditorName + ".exe";
        private TestContext context;

        private static FancyZonesEditorFiles? _files;

        public static FancyZonesEditorFiles Files
        {
            get
            {
                if (_files == null)
                {
                    _files = new FancyZonesEditorFiles();
                }

                return _files;
            }
        }

        public static class AccessibilityId
        {
            // main window
            public const string MainWindow = "MainWindow1";
            public const string Monitors = "Monitors";
            public const string NewLayoutButton = "NewLayoutButton";

            // layout card
            public const string EditLayoutButton = "EditLayoutButton";

            // edit layout window: common for template and custom layouts
            public const string SensitivitySlider = "SensitivityInput";
            public const string SpacingSlider = "Spacing";
            public const string SpacingToggle = "spaceAroundSetting";
            public const string HorizontalDefaultButtonUnchecked = "SetLayoutAsHorizontalDefaultButton";
            public const string VerticalDefaultButtonUnchecked = "SetLayoutAsVerticalDefaultButton";
            public const string HorizontalDefaultButtonChecked = "HorizontalDefaultLayoutButton";
            public const string VerticalDefaultButtonChecked = "VerticalDefaultLayoutButton";

            // edit template layout window
            public const string CopyTemplate = "createFromTemplateLayoutButton";
            public const string TemplateZoneSlider = "TemplateZoneCount";

            // edit custom layout window
            public const string DuplicateLayoutButton = "duplicateLayoutButton";
            public const string DeleteLayoutButton = "deleteLayoutButton";
            public const string KeySelectionComboBox = "quickKeySelectionComboBox";
            public const string EditZonesButton = "editZoneLayoutButton";
            public const string DeleteTextButton = "DeleteButton";
            public const string HotkeyComboBox = "quickKeySelectionComboBox";
            public const string NewZoneButton = "newZoneButton";
            public const string TopRightCorner = "NEResize";

            // layout creation dialog
            public const string GridRadioButton = "GridLayoutRadioButton";
            public const string CanvasRadioButton = "CanvasLayoutRadioButton";

            // confirmation dialog
            public const string PrimaryButton = "PrimaryButton";
            public const string SecondaryButton = "SecondaryButton";
        }

        public static class ElementName
        {
            // context menu
            public const string EditZones = "Edit zones";

            // canvas layout editor
            public const string CanvasEditorWindow = "Canvas layout editor";

            // grid layout editor
            public const string MergeZonesButton = "Merge zones";
        }

        public static class ClassName
        {
            // layout editor
            public const string CanvasZone = "CanvasZone";
            public const string GridZone = "GridZone";
        }

        public WindowsDriver<WindowsElement>? Session { get; }

        public FancyZonesEditorSession(TestContext testContext)
        {
            try
            {
                // Launch FancyZonesEditor
                string? path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                path += FancyZonesEditorPath;

                AppiumOptions opts = new AppiumOptions();
                opts.AddAdditionalCapability("app", path);
                Session = new WindowsDriver<WindowsElement>(new Uri(WindowsApplicationDriverUrl), opts);
            }
            catch (Exception ex)
            {
                testContext.WriteLine(ex.Message);
            }

            Assert.IsNotNull(Session, "Session not initialized");

            testContext.WriteLine("Session: " + Session.SessionId.ToString());
            testContext.WriteLine("Title: " + Session.Title);

            context = testContext;
        }

        public void Close()
        {
            // Close the session
            if (Session != null)
            {
                try
                {
                    // in case if something went wrong and an error message is shown
                    var dialog = Session.FindElementByName("Editor data parsing error.");
                    Session.CloseApp(); // will close the dialog

                    // session can't access new Editor instance created after closing the dialog
                    // kill the process
                    IntPtr appTopLevelWindowHandle = IntPtr.Zero;
                    foreach (Process clsProcess in Process.GetProcesses())
                    {
                        if (clsProcess.ProcessName.Equals(FancyZonesEditorName, StringComparison.OrdinalIgnoreCase))
                        {
                            clsProcess.Kill();
                            break;
                        }
                    }
                }
                catch
                {
                }

                try
                {
                    // FZEditor application can be closed by explicitly closing main editor window
                    var mainEditorWindow = Session.FindElementByAccessibilityId(AccessibilityId.MainWindow);
                    mainEditorWindow?.SendKeys(Keys.Alt + Keys.F4);
                }
                catch (Exception ex)
                {
                    context.WriteLine("Unable to close main window. ", ex.Message);
                }

                Session.Quit();
                Session.Dispose();
            }
        }

        public WindowsElement? GetLayout(string layoutName)
        {
            try
            {
                var listItem = Session?.FindElementByName(layoutName);
                return listItem;
            }
            catch (Exception)
            {
                context.WriteLine("Layout " + layoutName + " not found");
                return null;
            }
        }

        public WindowsElement OpenContextMenu(string layoutName)
        {
            RightClickLayout(layoutName);
            var menu = Session?.FindElementByClassName("ContextMenu");
            Assert.IsNotNull(menu, "Context menu not found");
            return menu;
        }

        public WindowsElement? GetMonitorsList()
        {
            return FindByAccessibilityId(AccessibilityId.Monitors);
        }

        public WindowsElement GetMonitorItem(int monitorNumber)
        {
            try
            {
                var monitorsList = GetMonitorsList();
                Assert.IsNotNull(monitorsList, "Monitors list not found");
                var listItem = monitorsList?.FindElementByName($"{monitorNumber}");
                Assert.IsNotNull(listItem, "Monitor " + monitorNumber + " not found");
                return (WindowsElement)listItem;
            }
            catch (Exception)
            {
                Assert.Fail("Monitor " + monitorNumber + " not found");
                return null;
            }
        }

        public WindowsElement? GetZoneCountSlider()
        {
            return FindByAccessibilityId(AccessibilityId.TemplateZoneSlider);
        }

        public WindowsElement? GetSensitivitySlider()
        {
            return FindByAccessibilityId(AccessibilityId.SensitivitySlider);
        }

        public WindowsElement? GetSpaceAroundZonesSlider()
        {
            return FindByAccessibilityId(AccessibilityId.SpacingSlider);
        }

        public WindowsElement? GetSpaceAroundZonesToggle()
        {
            return FindByAccessibilityId(AccessibilityId.SpacingToggle);
        }

        public WindowsElement? GetNameInput()
        {
            try
            {
                return Session?.FindElementByClassName("TextBox");
            }
            catch
            {
                Assert.Fail($"Name TextBox not found");
                return null;
            }
        }

        public WindowsElement? GetHotkeyComboBox()
        {
            return FindByAccessibilityId(AccessibilityId.HotkeyComboBox);
        }

        public WindowsElement? GetHotkeyPopup()
        {
            try
            {
                return Session?.FindElementByClassName("Popup");
            }
            catch
            {
                Assert.Fail($"Hotkey popup window not found");
                return null;
            }
        }

        public WindowsElement? GetHorizontalDefaultButton(bool isChecked)
        {
            if (isChecked)
            {
                return FindByAccessibilityId(AccessibilityId.HorizontalDefaultButtonChecked);
            }
            else
            {
                return FindByAccessibilityId(AccessibilityId.HorizontalDefaultButtonUnchecked);
            }
        }

        public WindowsElement? GetVerticalDefaultButton(bool isChecked)
        {
            if (isChecked)
            {
                return FindByAccessibilityId(AccessibilityId.VerticalDefaultButtonChecked);
            }
            else
            {
                return FindByAccessibilityId(AccessibilityId.VerticalDefaultButtonUnchecked);
            }
        }

        public WindowsElement? GetGridLayoutEditorWindow()
        {
            try
            {
                return Session?.FindElementByName("Grid layout editor");
            }
            catch
            {
                Assert.Fail($"Grid layout editor window not found");
                return null;
            }
        }

        public WindowsElement? GetZone(int zoneNumber, string zoneClassName)
        {
            ReadOnlyCollection<WindowsElement>? zones = null;

            try
            {
                zones = Session?.FindElementsByClassName(zoneClassName);
            }
            catch
            {
            }

            if (zones == null || zones.Count == 0)
            {
                return null;
            }

            foreach (WindowsElement zone in zones)
            {
                try
                {
                    zone.FindElementByName(zoneNumber.ToString(CultureInfo.InvariantCulture));
                    return zone;
                }
                catch
                {
                    // required number not found in the zone
                }
            }

            return null;
        }

        public void ClickCreateNewLayout()
        {
            var button = FindByAccessibilityId(AccessibilityId.NewLayoutButton);
            Assert.IsNotNull(button, "Create new layout button not found");
            button?.Click();
        }

        public void ClickEditLayout(string layoutName)
        {
            var layout = GetLayout(layoutName);
            Assert.IsNotNull(layout, $"Layout \"{layoutName}\" not found");

            // added retry attempts, because Click can fail for some reason
            bool opened = false;
            int retryAttempts = 10;
            while (!opened && retryAttempts > 0)
            {
                var editButton = layout?.FindElementByAccessibilityId(AccessibilityId.EditLayoutButton);
                Assert.IsNotNull(editButton, "Edit button not found");
                editButton.Click();

                // wait until the dialog is opened
                opened = WaitElementDisplayedByName($"Edit '{layoutName}'");
                retryAttempts--;
            }
        }

        public void ClickEditZones()
        {
            var button = FindByAccessibilityId(AccessibilityId.EditZonesButton);
            button?.Click();
        }

        public void RightClickLayout(string layoutName)
        {
            var layout = GetLayout(layoutName);
            ContextClick(layout!);
        }

        public void ClickMonitor(int monitorNumber)
        {
            var monitor = GetMonitorItem(monitorNumber);
            Click(monitor!);
        }

        public void ClickSave()
        {
            var button = Session?.FindElementByName("Save");
            Assert.IsNotNull(button, "No Save button");
            button.Click();
        }

        public void ClickCancel()
        {
            var button = Session?.FindElementByName("Cancel");
            Assert.IsNotNull(button, "No Cancel button");
            button.Click();
        }

        public void ClickCopyLayout()
        {
            WindowsElement? button = null;
            try
            {
                button = Session?.FindElementByAccessibilityId(AccessibilityId.CopyTemplate);
            }
            catch
            {
            }

            try
            {
                button = Session?.FindElementByAccessibilityId(AccessibilityId.DuplicateLayoutButton);
            }
            catch
            {
            }

            Assert.IsNotNull(button, "No Copy button");
            button.Click();
        }

        public void ClickDeleteLayout()
        {
            WindowsElement? button = Session?.FindElementByAccessibilityId(AccessibilityId.DeleteLayoutButton);
            button?.Click();
        }

        public void ClickConfirm()
        {
            WaitElementDisplayedById(AccessibilityId.PrimaryButton);
            WindowsElement? button = Session?.FindElementByAccessibilityId(AccessibilityId.PrimaryButton);
            button?.Click();
            WaitUntilHidden(button!);
        }

        public void ClickConfirmDialog()
        {
            Actions actions = new Actions(Session);
            actions.SendKeys(Keys.Tab).SendKeys(Keys.Enter);
            actions.Build().Perform();
        }

        public void ClickCancelDialog()
        {
            Actions actions = new Actions(Session);
            actions.SendKeys(Keys.Tab).SendKeys(Keys.Tab).SendKeys(Keys.Enter);
            actions.Build().Perform();
        }

        public void ClickContextMenuItem(string layoutName, string menuItem)
        {
            WindowsElement menu = OpenContextMenu(layoutName);
            Click(menu.FindElementByName(menuItem));
        }

        public void ClickAddNewZone()
        {
            var button = FindByAccessibilityId(AccessibilityId.NewZoneButton);
            button?.Click();
        }

        public void ClickDeleteZone(int zoneNumber)
        {
            var zone = GetZone(zoneNumber, ClassName.CanvasZone);
            Assert.IsNotNull(zone);
            var button = zone?.FindElementByClassName("Button");
            Assert.IsNotNull(button);
            button?.Click();
        }

        public void MergeGridZones(int zoneNumber1, int zoneNumber2)
        {
            var zone1 = GetZone(zoneNumber1, ClassName.GridZone);
            var zone2 = GetZone(zoneNumber2, ClassName.GridZone);
            if (zone1 == null || zone2 == null)
            {
                return;
            }

            Actions actions = new Actions(Session);
            actions.MoveToElement(zone1).ClickAndHold();
            int dx = (zone2.Rect.X - zone1.Rect.X) / 10;
            int dy = (zone2.Rect.Y - zone1.Rect.Y) / 10;
            for (int i = 0; i < 10; i++)
            {
                actions.MoveByOffset(dx, dy);
            }

            actions.MoveToElement(zone2).Release();
            actions.Build().Perform();

            Click(Session?.FindElementByName(ElementName.MergeZonesButton)!);
        }

        public void MoveSplitter(int xOffset, int yOffset)
        {
            ReadOnlyCollection<WindowsElement>? thumbs = Session?.FindElementsByClassName("Thumb");

            Actions actions = new Actions(Session);
            actions.MoveToElement(thumbs?[0]).ClickAndHold();
            int dx = xOffset / 10;
            int dy = yOffset / 10;
            for (int i = 0; i < 10; i++)
            {
                actions.MoveByOffset(dx, dy);
            }

            actions.Build().Perform();
        }

        public void SelectNewLayoutType(Constants.CustomLayoutType type)
        {
            WindowsElement? button = null;
            switch (type)
            {
                case Constants.CustomLayoutType.Canvas:
                    button = FindByAccessibilityId(AccessibilityId.CanvasRadioButton);
                    break;
                case Constants.CustomLayoutType.Grid:
                    button = FindByAccessibilityId(AccessibilityId.GridRadioButton);
                    break;
            }

            Click(button!);
        }

        private WindowsElement? FindByAccessibilityId(string name)
        {
            try
            {
                return Session?.FindElementByAccessibilityId(name);
            }
            catch (Exception)
            {
                context.WriteLine($"{name} not found");
                return null;
            }
        }

        public bool WaitElementDisplayedByName(string name)
        {
            try
            {
                WebDriverWait wait = new WebDriverWait(Session, TimeSpan.FromSeconds(1));
                return wait.Until(pred =>
                {
                    var element = Session?.FindElementByName(name);
                    if (element != null)
                    {
                        return element.Displayed;
                    }

                    return false;
                });
            }
            catch
            {
                return false;
            }
        }

        public void WaitElementDisplayedById(string id)
        {
            WebDriverWait wait = new WebDriverWait(Session, TimeSpan.FromSeconds(1));
            wait.Until(pred =>
            {
                var element = Session?.FindElementByAccessibilityId(id);
                if (element != null)
                {
                    return element.Displayed;
                }

                return false;
            });
        }

        public void WaitUntilHidden(WindowsElement element)
        {
            WebDriverWait wait = new WebDriverWait(Session, TimeSpan.FromSeconds(1));
            wait.Until(pred =>
            {
                return !element.Displayed;
            });
        }

        public void WaitFor(int seconds)
        {
           System.Threading.Thread.Sleep(seconds * 1000);
        }

        public void ContextClick(WindowsElement element)
        {
            Actions actions = new Actions(Session);
            actions.MoveToElement(element);
            actions.MoveByOffset(10, 10);
            actions.ContextClick();
            actions.Build().Perform();
        }

        public void Click(AppiumWebElement element)
        {
            Actions actions = new Actions(Session);
            actions.MoveToElement(element);
            actions.MoveByOffset(10, 10);
            actions.Click();
            actions.Build().Perform();
        }

        public void Drag(WindowsElement element, int offsetX, int offsetY)
        {
            Actions actions = new Actions(Session);
            actions.MoveToElement(element).MoveByOffset(10, 10).ClickAndHold(element).MoveByOffset(offsetX, offsetY).Release();
            actions.Build().Perform();
        }

        public void Drag(AppiumWebElement element, int offsetX, int offsetY)
        {
            Actions actions = new Actions(Session);
            actions.MoveToElement(element).MoveByOffset(10, 10).ClickAndHold(element).MoveByOffset(offsetX, offsetY).Release();
            actions.Build().Perform();
        }
    }
}
