using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium.Windows;
using OpenQA.Selenium.Interactions;

namespace PowerToysTests
{
    [TestClass]
    public class FancyZonesEditorCustomLayoutsTests : PowerToysSession
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

        private void OpenCustomLayouts()
        {
            WindowsElement customsTab = session.FindElementByName("Custom");
            customsTab.Click();
            string isSelected = customsTab.GetAttribute("SelectionItem.IsSelected");
            Assert.AreEqual("True", isSelected, "Custom tab cannot be opened");
        }

        private void OpenCreatorWindow(string tabName, string creatorWindowName)
        {
            string elementXPath = "//Text[@Name=\"" + tabName + "\"]";
            session.FindElementByXPath(elementXPath).Click();
            session.FindElementByAccessibilityId("EditCustomButton").Click();

            WindowsElement creatorWindow = session.FindElementByName(creatorWindowName);
            Assert.IsNotNull(creatorWindow, "Creator window didn't open");
        }

        private void SetLayoutName(string name)
        {
            WindowsElement textBox = session.FindElementByClassName("TextBox");
            textBox.Click();
            textBox.SendKeys(Keys.Control + "a");
            textBox.SendKeys(Keys.Backspace);
            textBox.SendKeys(name);
        }

        private void ZoneCountTest(int canvasZoneCount, int gridZoneCount)
        {
            Assert.AreEqual(canvasZoneCount, session.FindElementsByClassName("CanvasZone").Count);
            Assert.AreEqual(gridZoneCount, session.FindElementsByClassName("GridZone").Count);
        }

        private void CancelTest()
        {
            new Actions(session).MoveToElement(session.FindElementByXPath("//Text[@Name=\"Cancel\"]")).Click().Perform();
            ShortWait();

            Assert.AreEqual(_initialZoneSettings, File.ReadAllText(_zoneSettingsPath), "Settings were changed");
        }

        private void SaveTest(string type, string name, int zoneCount)
        {
            new Actions(session).MoveToElement(session.FindElementByName("Save and apply")).Click().Perform();
            ShortWait();

            JObject settings = JObject.Parse(File.ReadAllText(_zoneSettingsPath));
            Assert.AreEqual(name, settings["custom-zone-sets"][0]["name"]);
            Assert.AreEqual(settings["custom-zone-sets"][0]["uuid"], settings["devices"][0]["active-zoneset"]["uuid"]);
            Assert.AreEqual(type, settings["custom-zone-sets"][0]["type"]);
            Assert.AreEqual(zoneCount, settings["custom-zone-sets"][0]["info"]["zones"].ToObject<JArray>().Count);
        }

        [TestMethod]
        public void CreateCancel()
        {
            OpenCreatorWindow("Create new custom", "Custom layout creator");
            ZoneCountTest(0, 0);

            session.FindElementByAccessibilityId("newZoneButton").Click();
            ZoneCountTest(1, 0);

            CancelTest();
        }

        [TestMethod]
        public void CreateEmpty()
        {
            OpenCreatorWindow("Create new custom", "Custom layout creator");
            ZoneCountTest(0, 0);

            SaveTest("canvas", "Custom Layout 1", 0);
        }

        [TestMethod]
        public void CreateSingleZone()
        {
            OpenCreatorWindow("Create new custom", "Custom layout creator");
            ZoneCountTest(0, 0);

            session.FindElementByAccessibilityId("newZoneButton").Click();
            ZoneCountTest(1, 0);

            SaveTest("canvas", "Custom Layout 1", 1);
        }

        [TestMethod]
        public void CreateManyZones()
        {
            OpenCreatorWindow("Create new custom", "Custom layout creator");
            ZoneCountTest(0, 0);

            const int expectedZoneCount = 20;
            WindowsElement addButton = session.FindElementByAccessibilityId("newZoneButton");
            for (int i = 0; i < expectedZoneCount; i++)
            {
                addButton.Click();
            }

            ZoneCountTest(expectedZoneCount, 0);
            SaveTest("canvas", "Custom Layout 1", expectedZoneCount);
        }

        [TestMethod]
        public void CreateDeleteZone()
        {
            OpenCreatorWindow("Create new custom", "Custom layout creator");
            ZoneCountTest(0, 0);

            WindowsElement addButton = session.FindElementByAccessibilityId("newZoneButton");

            for (int i = 0; i < 10; i++)
            {
                //add zone
                addButton.Click();
                WindowsElement zone = session.FindElementByClassName("CanvasZone");
                Assert.IsNotNull(zone, "Zone was not created");
                Assert.IsTrue(zone.Displayed, "Zone was not displayed");

                //remove zone
                zone.FindElementByClassName("Button").Click();
            }

            ZoneCountTest(0, 0);
            CancelTest();
        }

        [TestMethod]
        public void CreateWithName()
        {
            OpenCreatorWindow("Create new custom", "Custom layout creator");
            string name = "My custom zone layout name";
            SetLayoutName(name);          
            SaveTest("canvas", name, 0);
        }

        [TestMethod]
        public void CreateWithEmptyName()
        {
            OpenCreatorWindow("Create new custom", "Custom layout creator");
            string name = "";
            SetLayoutName(name);
            SaveTest("canvas", name, 0);
        }

        [TestMethod]
        public void CreateWithUnicodeCharactersName()
        {
            OpenCreatorWindow("Create new custom", "Custom layout creator");
            string name = "ёÖ±¬āݾᵩὡ√ﮘﻹտ";
            SetLayoutName(name);
            SaveTest("canvas", name, 0);
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
            OpenCustomLayouts();
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
            catch (OpenQA.Selenium.WebDriverException)
            {
                //editor has already closed
            }

            ResetDefautZoneSettings();
            ExitPowerToys();
        }
    }
}