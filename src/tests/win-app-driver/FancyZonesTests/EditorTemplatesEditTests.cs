using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using OpenQA.Selenium.Appium.Windows;
using OpenQA.Selenium.Interactions;

namespace PowerToysTests
{
    [TestClass]
    public class FancyZonesEditorTemplatesEditTests : FancyZonesEditor
    {
        private void ChangeLayout()
        {
            new Actions(session).MoveToElement(session.FindElementByAccessibilityId("PART_TitleBar")).MoveByOffset(0, -50).Click().Perform();
        }

        private void CancelTest()
        {
            WindowsElement cancelButton = session.FindElementByXPath("//Window[@Name=\"FancyZones Editor\"]/Window/Button[@Name=\"Cancel\"]");
            new Actions(session).MoveToElement(cancelButton).Click().Perform();
            WaitSeconds(1);

            Assert.AreEqual(_defaultZoneSettings, File.ReadAllText(_zoneSettingsPath), "Settings were changed");
        }

        private void SaveTest()
        {
            new Actions(session).MoveToElement(session.FindElementByName("Save and apply")).Click().Perform();
            WaitSeconds(1);

            JObject settings = JObject.Parse(File.ReadAllText(_zoneSettingsPath));
            Assert.AreEqual("Custom Layout 1", settings["custom-zone-sets"][0]["name"]);
            Assert.AreEqual(settings["custom-zone-sets"][0]["uuid"], settings["devices"][0]["active-zoneset"]["uuid"]);
        }

        [TestMethod]
        public void EditFocusCancel()
        {
            OpenCreatorWindow("Focus", "Custom layout creator", "EditTemplateButton");
            ZoneCountTest(3, 0);

            session.FindElementByAccessibilityId("newZoneButton").Click();
            ZoneCountTest(4, 0);

            CancelTest();
        }

        [TestMethod]
        public void EditColumnsCancel()
        {
            OpenCreatorWindow("Columns", "Custom table layout creator", "EditTemplateButton");
            ZoneCountTest(0, 3);

            ChangeLayout();
            ZoneCountTest(0, 4);

            CancelTest();
        }

        [TestMethod]
        public void EditRowsCancel()
        {
            OpenCreatorWindow("Rows", "Custom table layout creator", "EditTemplateButton");
            ZoneCountTest(0, 3);

            ChangeLayout();
            ZoneCountTest(0, 4);

            CancelTest();
        }

        [TestMethod]
        public void EditGridCancel()
        {
            OpenCreatorWindow("Grid", "Custom table layout creator", "EditTemplateButton");
            ZoneCountTest(0, 3);

            ChangeLayout();
            ZoneCountTest(0, 4);

            CancelTest();
        }

        [TestMethod]
        public void EditPriorityGridCancel()
        {
            OpenCreatorWindow("Priority Grid", "Custom table layout creator", "EditTemplateButton");
            ZoneCountTest(0, 3);

            ChangeLayout();
            ZoneCountTest(0, 4);

            CancelTest();
        }

        [TestMethod]
        public void EditFocusSave()
        {
            OpenCreatorWindow("Focus", "Custom layout creator", "EditTemplateButton");
            ZoneCountTest(3, 0);

            session.FindElementByAccessibilityId("newZoneButton").Click();
            ZoneCountTest(4, 0);

            SaveTest();
        }

        [TestMethod]
        public void EditColumnsSave()
        {
            OpenCreatorWindow("Columns", "Custom table layout creator", "EditTemplateButton");
            ZoneCountTest(0, 3);

            ChangeLayout();
            ZoneCountTest(0, 4);

            SaveTest();
        }

        [TestMethod]
        public void EditRowsSave()
        {
            OpenCreatorWindow("Rows", "Custom table layout creator", "EditTemplateButton");
            ZoneCountTest(0, 3);

            ChangeLayout();
            ZoneCountTest(0, 4);

            SaveTest();
        }

        [TestMethod]
        public void EditGridSave()
        {
            OpenCreatorWindow("Grid", "Custom table layout creator", "EditTemplateButton");
            ZoneCountTest(0, 3);

            ChangeLayout();
            ZoneCountTest(0, 4);

            SaveTest();
        }

        [TestMethod]
        public void EditPriorityGridSave()
        {
            OpenCreatorWindow("Priority Grid", "Custom table layout creator", "EditTemplateButton");
            ZoneCountTest(0, 3);

            ChangeLayout();
            ZoneCountTest(0, 4);

            SaveTest();
        }

        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            Setup(context, false);
            ResetDefaultFancyZonesSettings(false);
            ResetDefautZoneSettings(true);
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