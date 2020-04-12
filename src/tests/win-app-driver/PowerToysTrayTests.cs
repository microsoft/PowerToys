using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;
using OpenQA.Selenium.Interactions;
using System.Diagnostics;

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
            ShortWait();

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
            isTrayOpened = true;

            //open PowerToys context menu
            WindowsElement pt = session.FindElementByName("PowerToys");
            Assert.IsNotNull(pt);

            new Actions(session).MoveToElement(pt).ContextClick().Perform();
            ShortWait();

            //open settings
            session.FindElementByXPath("//MenuItem[@Name=\"Settings\"]").Click();
            ShortWait();

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

            WindowsElement notificationOverflow = session.FindElementByName("Notification Overflow");
            AppiumWebElement overflowArea = notificationOverflow.FindElementByName("Overflow Notification Area");
            AppiumWebElement powerToys = overflowArea.FindElementByName("PowerToys");
            Assert.IsNotNull(powerToys);

            new Actions(session).MoveToElement(powerToys).ContextClick().Perform();
            ShortWait();

            //exit
            session.FindElementByXPath("//MenuItem[@Name=\"Exit\"]").Click();
            ShortWait();

            //check PowerToys exited
            powerToys = null;
            try
            {
                notificationOverflow = session.FindElementByName("Notification Overflow");
                overflowArea = notificationOverflow.FindElementByName("Overflow Notification Area");
                powerToys = overflowArea.FindElementByName("PowerToys");
            }
            catch (OpenQA.Selenium.WebDriverException)
            {
                //expected, PowerToys shouldn't be here
            }

            LaunchPowerToys();
            ShortWait();

            Assert.IsNull(powerToys);
        }

        [TestMethod]
        public void ImageResizerOpenWithContextMenu()
        {
            //open tray
            trayButton.Click();
            isTrayOpened = true;

            //open PowerToys context menu
            WindowsElement pt = session.FindElementByName("PowerToys");
            Assert.IsNotNull(pt);

            new Actions(session).MoveToElement(pt).ContextClick().Perform();
            ShortWait();

            //open Image Resizer window
            session.FindElementByXPath("//MenuItem[@Name=\"Image Resizer\"]").Click();
            ShortWait();

            //check Open File Dialog window opened
            WindowsElement imageResizerOpenWindow = session.FindElementByName("Image Resizer - Open files");
            Assert.IsNotNull(imageResizerOpenWindow);
            CloseWindow("Image Resizer - Open files");
            ShortWait();

            //check Image Resizer window opened
            Assert.AreNotEqual(Process.GetProcessesByName("ImageResizer").Length, 0);
            CloseWindowByProcessName("ImageResizer");
        }

        [TestMethod]
        public void WindowWalkerOpenWithContextMenu()
        {
            //open tray
            trayButton.Click();
            isTrayOpened = true;

            //open PowerToys context menu
            WindowsElement pt = session.FindElementByName("PowerToys");
            Assert.IsNotNull(pt);

            new Actions(session).MoveToElement(pt).ContextClick().Perform();
            ShortWait();

            //open Window Walker
            session.FindElementByXPath("//MenuItem[@Name=\"Window Walker\"]").Click();
            ShortWait();

            //check Window Walker opened
            Assert.AreNotEqual(Process.GetProcessesByName("WindowWalker").Length, 0);
            CloseWindowByProcessName("WindowWalker");
        }

        [TestMethod]
        public void AboutOpenWithContextMenu()
        {
            //open tray
            trayButton.Click();
            isTrayOpened = true;

            //open PowerToys context menu
            WindowsElement pt = session.FindElementByName("PowerToys");
            Assert.IsNotNull(pt);
            new Actions(session).MoveToElement(pt).ContextClick().Perform();
            ShortWait();

            //open About window
            session.FindElementByXPath("//MenuItem[@Name=\"About\"]").Click();
            ShortWait();

            //check About window opened
            WindowsElement aboutWindow = session.FindElementByName("About PowerToys");
            Assert.IsNotNull(aboutWindow);
            CloseWindow("About PowerToys");
        }

        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            Setup(context);
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
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
