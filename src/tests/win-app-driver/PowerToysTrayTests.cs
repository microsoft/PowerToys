using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium.Appium.Windows;
using System.Threading;

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
            Thread.Sleep(TimeSpan.FromSeconds(0.5));

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

            session.Mouse.ContextClick(pt.Coordinates);
            Thread.Sleep(TimeSpan.FromSeconds(0.5));

            //open settings
            session.FindElementByXPath("//MenuItem[@AutomationId=\"40002\"]").Click();
            Thread.Sleep(TimeSpan.FromSeconds(0.5));
            
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

            WindowsElement pt = session.FindElementByName("PowerToys");
            Assert.IsNotNull(pt);

            session.Mouse.ContextClick(pt.Coordinates);
            Thread.Sleep(TimeSpan.FromSeconds(0.5));

            //exit
            session.FindElementByXPath("//MenuItem[@AutomationId=\"40001\"]").Click();
            Thread.Sleep(TimeSpan.FromSeconds(0.5));

            //check PowerToys exited
            try
            {
                Assert.IsNull(session.FindElementByName("PowerToys"));
            }
            catch (OpenQA.Selenium.WebDriverException)
            {
                //expected, PowerToys shouldn't be here
            }

            LaunchPowerToys();
            Thread.Sleep(TimeSpan.FromSeconds(0.5));
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
