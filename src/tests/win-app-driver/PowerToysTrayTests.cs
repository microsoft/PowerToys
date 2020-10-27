using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;
using OpenQA.Selenium.Interactions;

namespace PowerToysTests
{
    [TestClass]
    public class PowerToysTrayTests : PowerToysSession
    {
        private bool isSettingsOpened;
        private bool isTrayOpened;

        [TestMethod]
        public void SettingsOpen()
        {
            OpenSettings();

            //check settings window opened
            WindowsElement settingsWindow = session.FindElementByName("PowerToys Settings");
            Assert.IsNotNull(settingsWindow);

            isSettingsOpened = true;
        }

        [TestMethod]
        public void SettingsOpenWithContextMenu()
        {
            //open tray
            trayButton.Click();
            WaitSeconds(1);
            isTrayOpened = true;

            //open PowerToys context menu
            AppiumWebElement pt = PowerToysTrayButton();
            Assert.IsNotNull(pt);

            new Actions(session).MoveToElement(pt).ContextClick().Perform();

            //open settings
            session.FindElementByXPath("//MenuItem[@Name=\"Settings\"]").Click();

            //check settings window opened
            WindowsElement settingsWindow = session.FindElementByName("PowerToys Settings");
            Assert.IsNotNull(settingsWindow);

            isSettingsOpened = true;
        }

        [TestMethod]
        public void PowerToysExit()
        {
            //open PowerToys context menu
            trayButton.Click();
            isTrayOpened = true;
            WaitSeconds(1);

            AppiumWebElement powerToys = PowerToysTrayButton();
            Assert.IsNotNull(powerToys);

            new Actions(session).MoveToElement(powerToys).ContextClick().Perform();

            //exit
            session.FindElementByAccessibilityId("40001").Click();

            //check PowerToys exited
            powerToys = null;
            try
            {
                powerToys = PowerToysTrayButton();
            }
            catch (OpenQA.Selenium.WebDriverException)
            {
                //expected, PowerToys shouldn't be here
            }

            LaunchPowerToys();
            Assert.IsNull(powerToys);
        }

        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            Setup(context);
            Assert.IsNotNull(session);

            if (!isPowerToysLaunched)
            {
                LaunchPowerToys();
            }
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
            isSettingsOpened = false;
            isTrayOpened = false;
        }

        [TestCleanup]
        public void TestCleanup()
        {
            if (isSettingsOpened)
            {
                CloseSettings();
            }

            if (isTrayOpened)
            {
                trayButton.Click();
            }
        }
    }
}
