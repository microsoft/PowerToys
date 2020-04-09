using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium.Windows;
using OpenQA.Selenium.Interactions;

namespace PowerToysTests
{
    [TestClass]
    public class FancyZonesEditorOpeningTwiceTest : PowerToysSession
    {
        [TestMethod]
        public void OpenEditorTwiceBySettingsButton()
        {
            ShortWait(); // Wait for the system
            OpenSettings();
            OpenFancyZonesSettings();
            session.FindElementByXPath("//Button[@Name=\"Edit zones\"]").Click(); // Click button Edit zones to open FancyZones Editor
            ShortWait(); // Wait for the system
            Assert.IsNotNull(session.FindElementByName("FancyZones Editor")); // Check the FancyZones Editor is running

            // Click button Edit zones while FancyZones Editor is running
            OpenSettings();
            session.FindElementByXPath("//Button[@Name=\"Edit zones\"]").Click(); // Click button Edit zones to open FancyZones Editor
            ShortWait(); // Wait for the system
            Assert.IsNotNull(session.FindElementByName("FancyZones Editor")); // Check the FancyZones Editor is running
            Assert.IsNotNull(session.FindElementByName("Fancy Zones Editor")); // Check the notification is running
            CloseWindow("Fancy Zones Editor"); // Close the notification
            OpenSettings();
            CloseWindow("FancyZones Editor"); // Close FancyZones Editor

            // Click button Edit zones while FancyZones Editor is not running
            session.FindElementByXPath("//Button[@Name=\"Edit zones\"]").Click(); // Click button Edit zones to open FancyZones Editor
            ShortWait(); // Wait for the system
            Assert.IsNotNull(session.FindElementByName("FancyZones Editor")); // Check the FancyZones Editor is running
            CloseWindow("FancyZones Editor"); // Close FancyZones Editor
            CloseSettings();
        }

        [TestMethod]
        public void OpenEditorTwiceByHotkey()
        {
            ShortWait(); // Wait for the system
            new Actions(session).KeyDown(Keys.Command).SendKeys("`").KeyUp(Keys.Command).Perform(); // Hold Win (Command) and press ` -> Open editor by hotkey
            ShortWait(); // Wait for the system
            Assert.IsNotNull(session.FindElementByName("FancyZones Editor")); // Check the FancyZones Editor is running

            // Open Edit zones while FancyZones Editor is running
            new Actions(session).KeyDown(Keys.Command).SendKeys("`").KeyUp(Keys.Command).Perform(); // Hold Win (Command) and press ` -> Open editor by hotkey
            ShortWait(); // Wait for the system
            Assert.IsNotNull(session.FindElementByName("FancyZones Editor")); // Check the FancyZones Editor is running
            Assert.IsNotNull(session.FindElementByName("Fancy Zones Editor")); // Check the notification is running
            CloseWindow("Fancy Zones Editor"); // Close the notification
            CloseWindow("FancyZones Editor"); // Close FancyZones Editor

            // Open Edit zones while FancyZones Editor is not running
            new Actions(session).KeyDown(Keys.Command).SendKeys("`").KeyUp(Keys.Command).Perform(); // Hold Win (Command) and press ` -> Open editor by hotkey
            ShortWait(); // Wait for the system
            Assert.IsNotNull(session.FindElementByName("FancyZones Editor")); // Check the FancyZones Editor is running
            CloseWindow("FancyZones Editor"); // Close FancyZones Editor
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

        }

        [TestCleanup]
        public void TestCleanup()
        {

        }
    }
}