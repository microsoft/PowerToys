using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium.Appium.Windows;

namespace PowerToysTests
{
    [TestClass]
    public class FancyZonesEditorTests : PowerToysSession
    {
        [TestMethod]
        public void OpenEditorBySettingsButton()
        {
            OpenFancyZonesSettings();

            WindowsElement editorButton = session.FindElementByXPath("//Button[@Name=\"Edit zones\"]");
            Assert.IsNotNull(editorButton);

            editorButton.Click();
            ShortWait();

            WindowsElement editorWindow = session.FindElementByXPath("//Window[@Name=\"FancyZones Editor\"]");
            Assert.IsNotNull(editorWindow);

            //Close editor
            session.FindElementByAccessibilityId("PART_Close").Click();
        }

        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            Setup(context);
            OpenSettings();
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
            
        }

        [TestCleanup]
        public void TestCleanup()
        {
            
        }
    }
}