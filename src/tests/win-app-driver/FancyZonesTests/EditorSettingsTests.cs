using OpenQA.Selenium;
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium.Appium.Windows;

namespace PowerToysTests
{
    [TestClass]
    public class FancyZonesEditorSettingsTests : PowerToysSession
    {
        [TestMethod]
        public void ZoneCount()
        {

            WaitSeconds(1);

            OpenFancyZonesSettings();

            WaitSeconds(1);

            WindowsElement editorButton = session.FindElementByXPath("//Button[@Name=\"Edit zones\"]");
            Assert.IsNotNull(editorButton);
            editorButton.Click();

            WaitSeconds(1);

            WindowsElement minusButton = session.FindElementByAccessibilityId("decrementZones");
            Assert.IsNotNull(minusButton);

            WindowsElement plusButton = session.FindElementByAccessibilityId("incrementZones");
            Assert.IsNotNull(plusButton);

            WindowsElement zoneCount = session.FindElementByAccessibilityId("zoneCount");
            Assert.IsNotNull(zoneCount);

            int zoneCountQty;
            Assert.IsTrue(Int32.TryParse(zoneCount.Text, out zoneCountQty));

            for (int i = zoneCountQty; i > -5; --i)
            {
                Assert.IsTrue(Int32.TryParse(zoneCount.Text, out zoneCountQty));
                Assert.AreEqual(Math.Max(i, 1), zoneCountQty);
                minusButton.Click();
            }

            for (int i = 1; i < 45; ++i)
            {
                Assert.IsTrue(Int32.TryParse(zoneCount.Text, out zoneCountQty));
                Assert.AreEqual(Math.Min(i, 40), zoneCountQty);
                plusButton.Click();
            }

            WindowsElement mainWindow = session.FindElementByAccessibilityId("MainWindow1");
            Assert.IsNotNull(mainWindow);
            mainWindow.SendKeys(Keys.Alt + Keys.F4);
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