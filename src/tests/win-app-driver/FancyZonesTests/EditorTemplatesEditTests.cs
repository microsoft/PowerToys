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

        private void ZoneCountTest(int canvasZonesCount, int gridZonesCount)
        {
            Assert.AreEqual(canvasZonesCount, session.FindElementsByClassName("CanvasZone").Count);
            Assert.AreEqual(gridZonesCount, session.FindElementsByClassName("GridZone").Count);
        }

        [TestMethod]
        public void EditFocusCancel()
        {
            OpenCreatorWindow("Focus", "Custom layout creator");
            ZoneCountTest(3, 0);

            session.FindElementByAccessibilityId("newZoneButton").Click();
            ZoneCountTest(4, 0);

            CancelTest();
        }

        [TestMethod]
        public void EditColumnsCancel()
        {
            OpenCreatorWindow("Columns", "Custom table layout creator");
            ZoneCountTest(0, 3);

            ChangeLayout();
            ZoneCountTest(0, 4);

            CancelTest();
        }

        [TestMethod]
        public void EditRowsCancel()
        {
            OpenCreatorWindow("Rows", "Custom table layout creator");
            ZoneCountTest(0, 3);

            ChangeLayout();
            ZoneCountTest(0, 4);

            CancelTest();
        }

        [TestMethod]
        public void EditGridCancel()
        {
            OpenCreatorWindow("Grid", "Custom table layout creator");
            ZoneCountTest(0, 3);

            ChangeLayout();
            ZoneCountTest(0, 4);

            CancelTest();
        }

        [TestMethod]
        public void EditPriorityGridCancel()
        {
            OpenCreatorWindow("Priority Grid", "Custom table layout creator");
            ZoneCountTest(0, 3);

            ChangeLayout();
            ZoneCountTest(0, 4);

            CancelTest();
        }

        [TestMethod]
        public void EditFocusSave()
        {
            OpenCreatorWindow("Focus", "Custom layout creator");
            ZoneCountTest(3, 0);

            session.FindElementByAccessibilityId("newZoneButton").Click();
            ZoneCountTest(4, 0);

            SaveTest();
        }

        [TestMethod]
        public void EditColumnsSave()
        {
            OpenCreatorWindow("Columns", "Custom table layout creator");
            ZoneCountTest(0, 3);

            ChangeLayout();
            ZoneCountTest(0, 4);

            SaveTest();
        }

        [TestMethod]
        public void EditRowsSave()
        {
            OpenCreatorWindow("Rows", "Custom table layout creator");
            ZoneCountTest(0, 3);

            ChangeLayout();
            ZoneCountTest(0, 4);

            SaveTest();
        }

        [TestMethod]
        public void EditGridSave()
        {
            OpenCreatorWindow("Grid", "Custom table layout creator");
            ZoneCountTest(0, 3);

            ChangeLayout();
            ZoneCountTest(0, 4);

            SaveTest();
        }

        [TestMethod]
        public void EditPriorityGridSave()
        {
            OpenCreatorWindow("Priority Grid", "Custom table layout creator");
            ZoneCountTest(0, 3);

            ChangeLayout();
            ZoneCountTest(0, 4);

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
            ResetDefaultFancyZonesSettings(true);
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

            ResetDefautZoneSettings(false);
        }
    }
}