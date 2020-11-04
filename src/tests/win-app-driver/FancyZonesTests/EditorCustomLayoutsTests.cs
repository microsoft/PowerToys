using System.IO.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;
using OpenQA.Selenium.Interactions;

namespace PowerToysTests
{
    [TestClass]
    public class FancyZonesEditorCustomLayoutsTests : FancyZonesEditor
    {
        private static readonly IFileSystem FileSystem = new FileSystem();
        private static readonly IFile File = FileSystem.File;

        private void SetLayoutName(string name)
        {
            AppiumWebElement textBox = creatorWindow.FindElementByClassName("TextBox");
            textBox.Click();
            textBox.SendKeys(Keys.Control + "a");
            textBox.SendKeys(Keys.Backspace);
            textBox.SendKeys(name);
        }

        private void CancelTest()
        {
            AppiumWebElement cancelButton = creatorWindow.FindElementByXPath("//Button[@Name=\"Cancel\"]");
            new Actions(session).MoveToElement(cancelButton).Click().Perform();
            WaitSeconds(1);

            Assert.AreEqual(_initialZoneSettings, File.ReadAllText(_zoneSettingsPath), "Settings were changed");
        }

        private void SaveTest(string type, string name, int zoneCount)
        {
            new Actions(session).MoveToElement(editorWindow.FindElementByName("Save and apply")).Click().Perform();
            WaitSeconds(1);

            JObject settings = JObject.Parse(File.ReadAllText(_zoneSettingsPath));
            Assert.AreEqual(name, settings["custom-zone-sets"][0]["name"]);
            Assert.AreEqual(settings["custom-zone-sets"][0]["uuid"], settings["devices"][0]["active-zoneset"]["uuid"]);
            Assert.AreEqual(type, settings["custom-zone-sets"][0]["type"]);
            Assert.AreEqual(zoneCount, settings["custom-zone-sets"][0]["info"]["zones"].ToObject<JArray>().Count);
        }

        [TestMethod]
        public void CreateCancel()
        {
            OpenCreatorWindow("Create new custom");
            ZoneCountTest(0, 0);

            editorWindow.FindElementByAccessibilityId("newZoneButton").Click();
            ZoneCountTest(1, 0);

            CancelTest();
        }

        [TestMethod]
        public void CreateEmpty()
        {
            OpenCreatorWindow("Create new custom");
            ZoneCountTest(0, 0);

            SaveTest("canvas", "Custom Layout 1", 0);
        }

        [TestMethod]
        public void CreateSingleZone()
        {
            OpenCreatorWindow("Create new custom");
            ZoneCountTest(0, 0);

            editorWindow.FindElementByAccessibilityId("newZoneButton").Click();
            ZoneCountTest(1, 0);

            SaveTest("canvas", "Custom Layout 1", 1);
        }

        [TestMethod]
        public void CreateManyZones()
        {
            OpenCreatorWindow("Create new custom");
            ZoneCountTest(0, 0);

            const int expectedZoneCount = 20;
            AppiumWebElement addButton = editorWindow.FindElementByAccessibilityId("newZoneButton");
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
            OpenCreatorWindow("Create new custom");
            ZoneCountTest(0, 0);

            AppiumWebElement addButton = editorWindow.FindElementByAccessibilityId("newZoneButton");

            for (int i = 0; i < 5; i++)
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
            OpenCreatorWindow("Create new custom");
            string name = "My custom zone layout name";
            SetLayoutName(name);
            SaveTest("canvas", name, 0);
        }

        [TestMethod]
        public void CreateWithEmptyName()
        {
            OpenCreatorWindow("Create new custom");
            string name = "";
            SetLayoutName(name);
            SaveTest("canvas", name, 0);
        }

        [TestMethod]
        public void CreateWithUnicodeCharactersName()
        {
            OpenCreatorWindow("Create new custom");
            string name = "ёÖ±¬āݾᵩὡ√ﮘﻹտ";
            SetLayoutName(name);
            SaveTest("canvas", name, 0);
        }

        [TestMethod]
        public void RenameLayout()
        {
            //create layout
            OpenCreatorWindow("Create new custom");
            string name = "My custom zone layout name";
            SetLayoutName(name);
            SaveTest("canvas", name, 0);
            WaitSeconds(1);

            //rename layout
            Assert.IsTrue(OpenEditor());
            OpenCustomLayouts();
            OpenCreatorWindow(name);
            name = "New name";
            SetLayoutName(name);
            SaveTest("canvas", name, 0);
        }

        [TestMethod]
        public void AddRemoveSameLayoutNames()
        {
            string name = "Name";

            for (int i = 0; i < 3; i++)
            {
                //create layout
                OpenCreatorWindow("Create new custom");
                SetLayoutName(name);

                new Actions(session).MoveToElement(editorWindow.FindElementByName("Save and apply")).Click().Perform();

                //remove layout
                Assert.IsTrue(OpenEditor());
                OpenCustomLayouts();
                AppiumWebElement nameLabel = editorWindow.FindElementByXPath("//Text[@Name=\"" + name + "\"]");
                new Actions(session).MoveToElement(nameLabel).MoveByOffset(nameLabel.Rect.Width / 2 + 10, 0).Click().Perform();
            }

            //settings are saved on window closing
            new Actions(session).MoveToElement(editorWindow.FindElementByAccessibilityId("PART_Close")).Click().Perform();
            WaitSeconds(1);

            //check settings
            JObject settings = JObject.Parse(File.ReadAllText(_zoneSettingsPath));
            Assert.AreEqual(0, settings["custom-zone-sets"].ToObject<JArray>().Count);
        }

        [TestMethod]
        public void RemoveApply()
        {
            string name = "Name";

            //create layout
            OpenCreatorWindow("Create new custom");
            SetLayoutName(name);
            new Actions(session).MoveToElement(editorWindow.FindElementByName("Save and apply")).Click().Perform();
            WaitSeconds(1);

            //save layout id
            JObject settings = JObject.Parse(File.ReadAllText(_zoneSettingsPath));
            Assert.AreEqual(1, settings["custom-zone-sets"].ToObject<JArray>().Count);

            //remove layout
            Assert.IsTrue(OpenEditor());
            OpenCustomLayouts();
            AppiumWebElement nameLabel = editorWindow.FindElementByXPath("//Text[@Name=\"" + name + "\"]");
            new Actions(session).MoveToElement(nameLabel).MoveByOffset(nameLabel.Rect.Width / 2 + 10, 0).Click().Perform();

            //apply
            new Actions(session).MoveToElement(editorWindow.FindElementByName("Apply")).Click().Perform();
            WaitSeconds(1);

            //check settings
            settings = JObject.Parse(File.ReadAllText(_zoneSettingsPath));
            Assert.AreEqual(0, settings["custom-zone-sets"].ToObject<JArray>().Count);
        }

        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            Setup(context);
            Assert.IsNotNull(session);
            EnableModules(false, true, false, false, false, false, false, false);
            
            ResetDefaultFancyZonesSettings(false);
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
            ExitPowerToys();
            TearDown();
        }

        [TestInitialize]
        public void TestInitialize()
        {
            ResetDefaultZoneSettings(true);
            Assert.IsTrue(OpenEditor());
            OpenCustomLayouts();
        }

        [TestCleanup]
        public void TestCleanup()
        {
            CloseEditor();
            ExitPowerToys();
        }
    }
}