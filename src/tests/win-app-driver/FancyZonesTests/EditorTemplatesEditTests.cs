using System.IO.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Interactions;

namespace PowerToysTests
{
    [TestClass]
    public class FancyZonesEditorTemplatesEditTests : FancyZonesEditor
    {
        private static readonly IFileSystem FileSystem = new FileSystem();
        private static readonly IFile File = FileSystem.File;
        private void ChangeLayout()
        {
            new Actions(session).MoveToElement(creatorWindow.FindElementByAccessibilityId("PART_TitleBar")).MoveByOffset(0, -50).Click().Perform();
        }

        private void Cancel(AppiumWebElement creatorWindow)
        {
            AppiumWebElement cancelButton = creatorWindow.FindElementByName("Cancel");
            Assert.IsNotNull(cancelButton);
            new Actions(session).MoveToElement(cancelButton).Click().Perform();
        }

        private void CancelTest(AppiumWebElement creatorWindow)
        {
            Cancel(creatorWindow);
            WaitSeconds(1);

            Assert.AreEqual(_defaultZoneSettings, File.ReadAllText(_zoneSettingsPath), "Settings were changed");
        }

        private void SaveTest()
        {
            new Actions(session).MoveToElement(creatorWindow.FindElementByName("Save and apply")).Click().Perform();
            WaitSeconds(1);

            JObject settings = JObject.Parse(File.ReadAllText(_zoneSettingsPath));
            Assert.AreEqual("Custom Layout 1", settings["custom-zone-sets"][0]["name"]);
            Assert.AreEqual(settings["custom-zone-sets"][0]["uuid"], settings["devices"][0]["active-zoneset"]["uuid"]);
        }

        [TestMethod]
        public void EditFocusCancel()
        {
            OpenCreatorWindow("Focus", "EditTemplateButton");
            ZoneCountTest(3, 0);

            creatorWindow.FindElementByAccessibilityId("newZoneButton").Click();
            ZoneCountTest(4, 0);

            CancelTest(creatorWindow);
        }

        [TestMethod]
        public void EditColumnsCancel()
        {
            OpenCreatorWindow("Columns", "EditTemplateButton");
            ZoneCountTest(0, 3);

            ChangeLayout();
            ZoneCountTest(0, 4);

            CancelTest(creatorWindow);
        }

        [TestMethod]
        public void EditRowsCancel()
        {
            OpenCreatorWindow("Rows", "EditTemplateButton");
            ZoneCountTest(0, 3);

            ChangeLayout();
            ZoneCountTest(0, 4);

            CancelTest(creatorWindow);
        }

        [TestMethod]
        public void EditGridCancel()
        {
            OpenCreatorWindow("Grid", "EditTemplateButton");
            ZoneCountTest(0, 3);

            ChangeLayout();
            ZoneCountTest(0, 4);

            CancelTest(creatorWindow);
        }

        [TestMethod]
        public void EditPriorityGridCancel()
        {
            OpenCreatorWindow("Priority Grid", "EditTemplateButton");
            ZoneCountTest(0, 3);

            ChangeLayout();
            ZoneCountTest(0, 4);

            CancelTest(creatorWindow);
        }

        [TestMethod]
        public void EditFocusSave()
        {
            OpenCreatorWindow("Focus", "EditTemplateButton");
            ZoneCountTest(3, 0);

            creatorWindow.FindElementByAccessibilityId("newZoneButton").Click();
            ZoneCountTest(4, 0);

            SaveTest();
        }

        [TestMethod]
        public void EditColumnsSave()
        {
            OpenCreatorWindow("Columns", "EditTemplateButton");
            ZoneCountTest(0, 3);

            ChangeLayout();
            ZoneCountTest(0, 4);

            SaveTest();
        }

        [TestMethod]
        public void EditRowsSave()
        {
            OpenCreatorWindow("Rows", "EditTemplateButton");
            ZoneCountTest(0, 3);

            ChangeLayout();
            ZoneCountTest(0, 4);

            SaveTest();
        }

        [TestMethod]
        public void EditGridSave()
        {
            OpenCreatorWindow("Grid", "EditTemplateButton");
            ZoneCountTest(0, 3);

            ChangeLayout();
            ZoneCountTest(0, 4);

            SaveTest();
        }

        [TestMethod]
        public void EditPriorityGridSave()
        {
            OpenCreatorWindow("Priority Grid", "EditTemplateButton");
            ZoneCountTest(0, 3);

            ChangeLayout();
            ZoneCountTest(0, 4);

            SaveTest();
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
            CloseSettings();
            ExitPowerToys();
            TearDown();
        }

        [TestInitialize]
        public void TestInitialize()
        {
            ResetDefaultZoneSettings(true);
            Assert.IsTrue(OpenEditor());
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
            catch (OpenQA.Selenium.WebDriverException)
            {
                //editor was already closed
            }
        }
    }
}