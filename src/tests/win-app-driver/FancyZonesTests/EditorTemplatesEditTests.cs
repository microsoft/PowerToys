using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium.Appium.Windows;
using OpenQA.Selenium.Interactions;

namespace PowerToysTests
{
    [TestClass]
    public class FancyZonesEditorTemplatesEditTests : PowerToysSession
    {
        WindowsElement editorWindow;

        static void ResetDefaultFancyZonesSettings()
        {
            if (!Directory.Exists(_settingsFolderPath))
            {
                Directory.CreateDirectory(_settingsFolderPath);
            }

            string settings = "{\"version\":\"1.0\",\"name\":\"FancyZones\",\"properties\":{\"fancyzones_shiftDrag\":{\"value\":true},\"fancyzones_overrideSnapHotkeys\":{\"value\":false},\"fancyzones_zoneSetChange_flashZones\":{\"value\":false},\"fancyzones_displayChange_moveWindows\":{\"value\":false},\"fancyzones_zoneSetChange_moveWindows\":{\"value\":false},\"fancyzones_virtualDesktopChange_moveWindows\":{\"value\":false},\"fancyzones_appLastZone_moveWindows\":{\"value\":false},\"use_cursorpos_editor_startupscreen\":{\"value\":true},\"fancyzones_zoneHighlightColor\":{\"value\":\"#0078D7\"},\"fancyzones_highlight_opacity\":{\"value\":90},\"fancyzones_editor_hotkey\":{\"value\":{\"win\":true,\"ctrl\":false,\"alt\":false,\"shift\":false,\"code\":192,\"key\":\"`\"}},\"fancyzones_excluded_apps\":{\"value\":\"\"}}}";
            File.WriteAllText(_settingsPath, settings);
        }

        private void OpenEditor()
        {
            new Actions(session).KeyDown(OpenQA.Selenium.Keys.Command).SendKeys("`").KeyUp(OpenQA.Selenium.Keys.Command).Perform();
            ShortWait();

            editorWindow = session.FindElementByXPath("//Window[@Name=\"FancyZones Editor\"]");
        }

        private void OpenTemplates()
        {
            WindowsElement templatesTab = session.FindElementByName("Templates");
            templatesTab.Click();
            string isSelected = templatesTab.GetAttribute("SelectionItem.IsSelected");
            Assert.AreEqual("True", isSelected, "Templates tab cannot be opened");
        }

        private void OpenCreatorWindow(string tabName, string creatorWindowName)
        {
            string elementXPath = "//Text[@Name=\"" + tabName + "\"]";
            session.FindElementByXPath(elementXPath).Click();
            session.FindElementByAccessibilityId("EditTemplateButton").Click();
            
            WindowsElement creatorWindow = session.FindElementByName(creatorWindowName);
            Assert.IsNotNull(creatorWindow, "Creator window didn't open");
        }

        private void ChangeLayout()
        {
            new Actions(session).MoveToElement(session.FindElementByAccessibilityId("PART_TitleBar")).MoveByOffset(0, -50).Click().Perform();
        }

        private void CancelTest()
        {
            new Actions(session).MoveToElement(session.FindElementByName("Cancel")).Click().Perform();
            ShortWait();

            Assert.AreEqual(_initialZoneSettings, File.ReadAllText(_zoneSettingsPath), "Settings were changed");
        }

        [TestMethod]
        public void EditFocusCancel()
        {
            OpenCreatorWindow("Focus", "Custom layout creator");
            session.FindElementByAccessibilityId("newZoneButton").Click();
            CancelTest();
        }

        [TestMethod]
        public void EditColumnsCancel()
        {
            OpenCreatorWindow("Columns", "Custom table layout creator");
            ChangeLayout();
            CancelTest();
        }

        [TestMethod]
        public void EditRowsCancel()
        {
            OpenCreatorWindow("Rows", "Custom table layout creator");
            ChangeLayout();
            CancelTest();
        }

        [TestMethod]
        public void EditGridCancel()
        {
            OpenCreatorWindow("Grid", "Custom table layout creator");
            ChangeLayout();
            CancelTest();
        }

        [TestMethod]
        public void EditPriorityGridCancel()
        {
            OpenCreatorWindow("Priority Grid", "Custom table layout creator");
            ChangeLayout();
            CancelTest();
        }

        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            Setup(context, false);

            if (isPowerToysLaunched)
            {
                ExitPowerToys();
            }
            ResetDefaultFancyZonesSettings();
            LaunchPowerToys();
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
            CloseSettings();
            //ExitPowerToys();
            TearDown();
        }

        [TestInitialize]
        public void TestInitialize()
        {
            OpenEditor();
            OpenTemplates();
        }

        [TestCleanup]
        public void TestCleanup()
        {
            //Close editor
            if (editorWindow != null)
            {
                editorWindow.SendKeys(OpenQA.Selenium.Keys.Alt + OpenQA.Selenium.Keys.F4);
                ShortWait();
            }
        }
    }
}