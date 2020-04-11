using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium.Appium.Windows;
using OpenQA.Selenium.Interactions;
using System.Diagnostics;

namespace PowerToysTests
{
    [TestClass]
    public class PowerToysClosingTests : PowerToysSession
    {
        private bool isTrayOpened;

        [TestMethod]
        public void ChildProcessesClosedWhenPowerToysExit()
        {
            ShortWait(); // Wait for the system
            OpenSettings();
            OpenFancyZonesSettings();
            session.FindElementByXPath("//Button[@Name=\"Edit zones\"]").Click(); // Click button Edit zones to open FancyZones Editor
            ShortWait(); // Wait for the system

            //check PowerToys is running
            Assert.AreNotEqual(Process.GetProcessesByName("PowerToys").Length, 0);

            //check PowerToysSettings window opened
            Assert.AreNotEqual(Process.GetProcessesByName("PowerToysSettings").Length, 0);

            //check Editor window opened
            Assert.AreNotEqual(Process.GetProcessesByName("FancyZonesEditor").Length, 0);


            //open PowerToys context menu
            trayButton.Click();
            isTrayOpened = true;

            WindowsElement pt = session.FindElementByName("PowerToys");
            Assert.IsNotNull(pt);

            new Actions(session).MoveToElement(pt).ContextClick().Perform();
            ShortWait();

            //exit
            session.FindElementByXPath("//MenuItem[@Name=\"Exit\"]").Click();
            ShortWait();

            //check PowerToys exited
            Assert.AreEqual(Process.GetProcessesByName("PowerToys").Length, 0);

            //check PowerToysSettings exited
            Assert.AreEqual(Process.GetProcessesByName("PowerToysSettings").Length, 0);

            //check Editor window exited
            Assert.AreEqual(Process.GetProcessesByName("FancyZonesEditor").Length, 0);
        }

        [TestMethod]
        public void EditorOpenedByHotkeyClosedWhenPowerToysExit()
        {
            ShortWait(); // Wait for the system

            //check Editor window is not opened
            Assert.AreEqual(Process.GetProcessesByName("FancyZonesEditor").Length, 0);

            //open Editor window by using hotkey Win (Command) + `
            new Actions(session).KeyDown(OpenQA.Selenium.Keys.Command).SendKeys("`").KeyUp(OpenQA.Selenium.Keys.Command).Perform();
            ShortWait();

            //check Editor window opened
            Assert.AreNotEqual(Process.GetProcessesByName("FancyZonesEditor").Length, 0);

            ////open PowerToys context menu
            trayButton.Click();
            isTrayOpened = true;

            WindowsElement pt = session.FindElementByName("PowerToys");
            Assert.IsNotNull(pt);

            new Actions(session).MoveToElement(pt).ContextClick().Perform();
            ShortWait();

            //exit
            session.FindElementByXPath("//MenuItem[@Name=\"Exit\"]").Click();
            ShortWait();

            //check PowerToys exited
            Assert.AreEqual(Process.GetProcessesByName("PowerToys").Length, 0);

            //check Editor window exited
            Assert.AreEqual(Process.GetProcessesByName("FancyZonesEditor").Length, 0);
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
            isTrayOpened = false;
        }

        [TestCleanup]
        public void TestCleanup()
        {
            if (isTrayOpened)
            {
                trayButton.Click();
            }

            //Process.Start(@"C:\Users\[username]\source\repos\PowerToys\x64\Debug\PowerToys.exe"); //Use for temporary testing to launch PowerToys
            LaunchPowerToys();
        }
    }
}
