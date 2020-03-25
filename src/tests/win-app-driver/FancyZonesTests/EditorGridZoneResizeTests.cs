using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium.Appium.Windows;
using OpenQA.Selenium.Interactions;
using System.Windows.Forms;

namespace PowerToysTests
{
    [TestClass]
    public class FancyZonesEditorGridZoneResizeTests : FancyZonesEditor
    {
        [TestMethod]
        public void MoveVerticalDelimiter()
        {
            OpenCreatorWindow("Columns", "Custom layout creator", "EditTemplateButton");
        }

        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            Setup(context, false);
            ResetSettings();

            if (!isPowerToysLaunched)
            {
                LaunchPowerToys();
            }
            OpenEditor();
            OpenTemplates();
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
            CloseEditor();
            TearDown();
        }

        [TestInitialize]
        public void TestInitialize()
        {

        }

        [TestCleanup]
        public void TestCleanup()
        {
            new Actions(session).MoveToElement(session.FindElementByXPath("//Button[@Name=\"Cancel\"]")).Click().Perform();
        }
    }
}