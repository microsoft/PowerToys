using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace PowerToysTests
{
    [TestClass]
    public class FancyZonesEditorTemplatesApplyTests : FancyZonesEditor
    {
        private void ApplyLayout(string tabName)
        {
            string elementXPath = "//Text[@Name=\"" + tabName + "\"]";
            session.FindElementByXPath(elementXPath).Click();
            session.FindElementByAccessibilityId("ApplyTemplateButton").Click();

            try
            {
                Assert.IsNull(session.FindElementByXPath("//Window[@Name=\"FancyZones Editor\"]"));
            }
            catch (OpenQA.Selenium.WebDriverException)
            {
                //editor was closed as expected
            }
        }

        private void CheckSettingsLayout(string expectedLayout)
        {
            JObject settings = JObject.Parse(File.ReadAllText(_zoneSettingsPath));
            Assert.AreEqual(expectedLayout, settings["devices"][0]["active-zoneset"]["type"]);
        }

        [TestMethod]
        public void ApplyFocus()
        {
            ApplyLayout("Focus");
            CheckSettingsLayout("focus");
        }

        [TestMethod]
        public void ApplyColumns()
        {
            ApplyLayout("Columns");
            CheckSettingsLayout("columns");
        }

        [TestMethod]
        public void ApplyRows()
        {
            ApplyLayout("Rows");
            CheckSettingsLayout("rows");
        }

        [TestMethod]
        public void ApplyGrid()
        {
            ApplyLayout("Grid");
            CheckSettingsLayout("grid");
        }

        [TestMethod]
        public void ApplyPriorityGrid()
        {
            ApplyLayout("Priority Grid");
            CheckSettingsLayout("priority-grid");
        }

        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            Setup(context, false);
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
            OpenEditor();
            OpenTemplates();
        }

        [TestCleanup]
        public void TestCleanup()
        {
        }
    }
}