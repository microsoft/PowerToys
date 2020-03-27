using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium.Windows;
using OpenQA.Selenium.Interactions;

namespace PowerToysTests
{
    [TestClass]
    public class FancyZonesEditorCustomLayoutsTests : FancyZonesEditor
    {       
        private void SetLayoutName(string name)
        {
            WindowsElement textBox = session.FindElementByClassName("TextBox");
            textBox.Click();
            textBox.SendKeys(Keys.Control + "a");
            textBox.SendKeys(Keys.Backspace);
            textBox.SendKeys(name);
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

        [TestMethod]
        public void RenameLayout()
        {
            //create layout
            OpenCreatorWindow("Create new custom", "Custom layout creator");
            string name = "My custom zone layout name";
            SetLayoutName(name);
            SaveTest("canvas", name, 0);
            ShortWait();

            //rename layout
            OpenEditor();
            OpenCustomLayouts();
            OpenCreatorWindow(name, "Custom layout creator");
            name = "New name";
            SetLayoutName(name);
            SaveTest("canvas", name, 0);
        }

        [TestMethod]
        public void RemoveLayout()
        {
            //create layout
            OpenCreatorWindow("Create new custom", "Custom layout creator");
            string name = "Name";
            SetLayoutName(name);
            SaveTest("canvas", name, 0);
            ShortWait();

            //save layout id
            JObject settings = JObject.Parse(File.ReadAllText(_zoneSettingsPath));
            Assert.AreEqual(1, settings["custom-zone-sets"].ToObject<JArray>().Count);
            string layoutId = settings["custom-zone-sets"][0]["uuid"].ToString();

            //remove layout
            OpenEditor();
            OpenCustomLayouts();
            WindowsElement nameLabel = session.FindElementByXPath("//Text[@Name=\"" + name + "\"]");
            new Actions(session).MoveToElement(nameLabel).MoveByOffset(nameLabel.Rect.Width / 2 + 10, 0).Click().Perform();

            //settings are saved on window closing
            new Actions(session).MoveToElement(session.FindElementByAccessibilityId("PART_Close")).Click().Perform();
            ShortWait();

            //check settings
            settings = JObject.Parse(File.ReadAllText(_zoneSettingsPath));
            Assert.AreEqual(0, settings["custom-zone-sets"].ToObject<JArray>().Count);
            foreach (JObject device in settings["devices"].ToObject<JArray>())
            {
                Assert.AreNotEqual(layoutId, device["active-zoneset"]["uuid"], "Deleted layout still applied");
            }
        }

        [TestMethod]
        public void AddRemoveSameLayoutNames()
        {
            string name = "Name";

            for (int i = 0; i < 3; i++)
            {
                //create layout
                OpenCreatorWindow("Create new custom", "Custom layout creator");
                SetLayoutName(name);

                new Actions(session).MoveToElement(session.FindElementByName("Save and apply")).Click().Perform();
                ShortWait();

                //remove layout
                OpenEditor();
                OpenCustomLayouts();
                WindowsElement nameLabel = session.FindElementByXPath("//Text[@Name=\"" + name + "\"]");
                new Actions(session).MoveToElement(nameLabel).MoveByOffset(nameLabel.Rect.Width / 2 + 10, 0).Click().Perform();
            }

            //settings are saved on window closing
            new Actions(session).MoveToElement(session.FindElementByAccessibilityId("PART_Close")).Click().Perform();
            ShortWait();

            //check settings
            JObject settings = JObject.Parse(File.ReadAllText(_zoneSettingsPath));
            Assert.AreEqual(0, settings["custom-zone-sets"].ToObject<JArray>().Count);
        }

        [TestMethod]
        public void AddRemoveDifferentLayoutNames()
        {
            for (int i = 0; i < 3; i++)
            {
                string name = i.ToString();

                //create layout
                OpenCreatorWindow("Create new custom", "Custom layout creator");
                SetLayoutName(name);

                new Actions(session).MoveToElement(session.FindElementByName("Save and apply")).Click().Perform();
                ShortWait();

                //remove layout
                OpenEditor();
                OpenCustomLayouts();
                WindowsElement nameLabel = session.FindElementByXPath("//Text[@Name=\"" + name + "\"]");
                new Actions(session).MoveToElement(nameLabel).MoveByOffset(nameLabel.Rect.Width / 2 + 10, 0).Click().Perform();
            }

            //settings are saved on window closing
            new Actions(session).MoveToElement(session.FindElementByAccessibilityId("PART_Close")).Click().Perform();
            ShortWait();

            //check settings
            JObject settings = JObject.Parse(File.ReadAllText(_zoneSettingsPath));
            Assert.AreEqual(0, settings["custom-zone-sets"].ToObject<JArray>().Count);
        }

        [TestMethod]
        public void RemoveApply()
        {
            string name = "Name";

            //create layout
            OpenCreatorWindow("Create new custom", "Custom layout creator");
            SetLayoutName(name);
            new Actions(session).MoveToElement(session.FindElementByName("Save and apply")).Click().Perform();
            ShortWait();

            //save layout id
            JObject settings = JObject.Parse(File.ReadAllText(_zoneSettingsPath));
            Assert.AreEqual(1, settings["custom-zone-sets"].ToObject<JArray>().Count);
            string layoutId = settings["custom-zone-sets"][0]["uuid"].ToString();

            //remove layout
            OpenEditor();
            OpenCustomLayouts();
            WindowsElement nameLabel = session.FindElementByXPath("//Text[@Name=\"" + name + "\"]");
            new Actions(session).MoveToElement(nameLabel).MoveByOffset(nameLabel.Rect.Width / 2 + 10, 0).Click().Perform();

            //apply
            new Actions(session).MoveToElement(session.FindElementByName("Apply")).Click().Perform();
            ShortWait();

            //check settings
            settings = JObject.Parse(File.ReadAllText(_zoneSettingsPath));
            Assert.AreEqual(0, settings["custom-zone-sets"].ToObject<JArray>().Count);
            foreach (JObject device in settings["devices"].ToObject<JArray>())
            {
                Assert.AreNotEqual(layoutId, device["active-zoneset"]["uuid"], "Deleted layout still applied");
            }
        }

        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            Setup(context, false);
            ResetSettings();
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
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
            CloseEditor();
            ResetDefautZoneSettings(false);
        }
    }
}