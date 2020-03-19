using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using OpenQA.Selenium.Appium.Windows;
using OpenQA.Selenium.Interactions;

namespace PowerToysTests
{
    [TestClass]
    public class FancyZonesEditorTemplatesEditTests : PowerToysSession
    {
        WindowsElement editorWindow;

        private static void ResetDefaultFancyZonesSettings()
        {
            if (!Directory.Exists(_settingsFolderPath))
            {
                Directory.CreateDirectory(_settingsFolderPath);
            }

            string settings = "{\"version\":\"1.0\",\"name\":\"FancyZones\",\"properties\":{\"fancyzones_shiftDrag\":{\"value\":true},\"fancyzones_overrideSnapHotkeys\":{\"value\":false},\"fancyzones_zoneSetChange_flashZones\":{\"value\":false},\"fancyzones_displayChange_moveWindows\":{\"value\":false},\"fancyzones_zoneSetChange_moveWindows\":{\"value\":false},\"fancyzones_virtualDesktopChange_moveWindows\":{\"value\":false},\"fancyzones_appLastZone_moveWindows\":{\"value\":false},\"use_cursorpos_editor_startupscreen\":{\"value\":true},\"fancyzones_zoneHighlightColor\":{\"value\":\"#0078D7\"},\"fancyzones_highlight_opacity\":{\"value\":90},\"fancyzones_editor_hotkey\":{\"value\":{\"win\":true,\"ctrl\":false,\"alt\":false,\"shift\":false,\"code\":192,\"key\":\"`\"}},\"fancyzones_excluded_apps\":{\"value\":\"\"}}}";
            File.WriteAllText(_settingsPath, settings);
        }

        private void ResetDefautZoneSettings()
        {
            string zoneSettings = "{\"app-zone-history\":[],\"devices\":[],\"custom-zone-sets\":[]}";
            File.WriteAllText(_zoneSettingsPath, zoneSettings);
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
            new Actions(session).MoveToElement(session.FindElementByXPath("//Button[@Name=\"Cancel\"]")).Click().Perform();
            ShortWait();

            Assert.AreEqual(_initialZoneSettings, File.ReadAllText(_zoneSettingsPath), "Settings were changed");
        }

        private void SaveTest()
        {
            new Actions(session).MoveToElement(session.FindElementByName("Save and apply")).Click().Perform();
            ShortWait();

            JObject settings = JObject.Parse(File.ReadAllText(_zoneSettingsPath));
            Assert.AreEqual("Custom Layout 1", settings["custom-zone-sets"][0]["name"]);
            Assert.AreEqual(settings["custom-zone-sets"][0]["uuid"], settings["devices"][0]["active-zoneset"]["uuid"]);
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

        [TestMethod]
        public void EditFocusSave()
        {
            OpenCreatorWindow("Focus", "Custom layout creator");
            session.FindElementByAccessibilityId("newZoneButton").Click();
            SaveTest();
        }

        [TestMethod]
        public void EditColumnsSave()
        {
            OpenCreatorWindow("Columns", "Custom table layout creator");
            ChangeLayout();
            SaveTest();
        }

        [TestMethod]
        public void EditRowsSave()
        {
            OpenCreatorWindow("Rows", "Custom table layout creator");
            ChangeLayout();
            SaveTest();
        }

        [TestMethod]
        public void EditGridSave()
        {
            OpenCreatorWindow("Grid", "Custom table layout creator");
            ChangeLayout();
            SaveTest();
        }

        [TestMethod]
        public void EditPriorityGridSave()
        {
            OpenCreatorWindow("Priority Grid", "Custom table layout creator");
            ChangeLayout();
            SaveTest();
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
            TearDown();
        }

        [TestInitialize]
        public void TestInitialize()
        {
            if (!isPowerToysLaunched)
            {
                LaunchPowerToys();
            }
            OpenEditor();
            OpenTemplates();
        }

        [TestCleanup]
        public void TestCleanup()
        {
            //Close editor
            try
            {
                if (editorWindow != null)
                {
                    editorWindow.SendKeys(OpenQA.Selenium.Keys.Alt + OpenQA.Selenium.Keys.F4);
                    ShortWait();
                }
            }
            catch(OpenQA.Selenium.WebDriverException)
            {
                //editor has already closed
            }

            ResetDefautZoneSettings();
            ExitPowerToys();
        }
    }
}