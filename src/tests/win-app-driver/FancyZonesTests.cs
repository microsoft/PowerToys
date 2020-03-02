using System;
using System.IO;
using Newtonsoft.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium.Windows;
using System.Threading;

namespace PowerToysTests
{
    [TestClass]
    public class FancyZonesTests : PowerToysSession
    {
        private string settingsFile = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft/PowerToys/FancyZones/settings.json");

        private void OpenFancyZonesSettings()
        {
            WindowsElement fzMenuButton = session.FindElementByXPath("//Button[@Name=\"FancyZones\"]");
            Assert.IsNotNull(fzMenuButton);
            fzMenuButton.Click();
            fzMenuButton.Click();

            Thread.Sleep(TimeSpan.FromSeconds(0.5));
        }

        [TestMethod]
        public void FancyZonesSettingsOpen()
        {
            WindowsElement fzMenuButton = session.FindElementByXPath("//Button[@Name=\"FancyZones\"]");
            Assert.IsNotNull(fzMenuButton);
            fzMenuButton.Click();
            fzMenuButton.Click();
            Thread.Sleep(TimeSpan.FromSeconds(0.5));

            WindowsElement fzTitle = session.FindElementByName("FancyZones Settings");
            Assert.IsNotNull(fzTitle);
        }

        [TestMethod]
        public void EditorOpen()
        {
            OpenFancyZonesSettings();

            session.FindElementByXPath("//Button[@Name=\"Edit zones\"]").Click();
            Thread.Sleep(TimeSpan.FromSeconds(0.5));

            WindowsElement editorWindow = session.FindElementByName("FancyZones Editor");
            Assert.IsNotNull(editorWindow);
        }

        [TestMethod]
        /*
         * click each toggle twice,
         * save changes,
         * check if settings are unchanged after clicking save button
         */
        [TestMethod]
        public void TogglesDoubleClickSave()
        {
            OpenFancyZonesSettings();

            WindowsElement saveButton = session.FindElementByName("Save");
            Assert.IsNotNull(saveButton);
            string isEnabled = saveButton.GetAttribute("IsEnabled");
            Assert.AreEqual("False", isEnabled);

            string initialSettings = File.ReadAllText(settingsFile);

            for (int i = 37; i < 45; i++)
            {
                string toggleId = "Toggle" + i.ToString();
                WindowsElement toggle = session.FindElementByAccessibilityId(toggleId);
                Assert.IsNotNull(toggle);
                toggle.Click();
                toggle.Click();

                isEnabled = saveButton.GetAttribute("IsEnabled");
                Assert.AreEqual("True", isEnabled);

                saveButton.Click();
                isEnabled = saveButton.GetAttribute("IsEnabled");
                Assert.AreEqual("False", isEnabled);

                string savedSettings = File.ReadAllText(settingsFile);
                Assert.AreEqual(initialSettings, savedSettings);
            }
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
            OpenSettings();
            Thread.Sleep(TimeSpan.FromSeconds(0.5));

            WindowsElement settingsWindow = session.FindElementByName("PowerToys Settings");
            settingsWindow.Click(); //set focus on window
        }

        [TestCleanup]
        public void TestCleanup()
        {
            try
            {
                WindowsElement editorWindow = session.FindElementByName("FancyZones Editor");
                if (editorWindow != null)
                {
                    editorWindow.SendKeys(Keys.Alt + Keys.F4);
                }
            } 
            catch (OpenQA.Selenium.WebDriverException)
            {
                //editor window not found
            }
                      

            CloseSettings();
        }
    }
}
