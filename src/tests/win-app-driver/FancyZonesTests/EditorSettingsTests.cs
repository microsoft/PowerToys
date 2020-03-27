using System;
using System.IO;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium.Windows;

namespace PowerToysTests
{
    [TestClass]
    public class FancyZonesEditorSettingsTests : PowerToysSession
    {
        private string editorZoneCount = "editor-zone-count";
        private string editorShowSpacing = "editor-show-spacing";
        private string editorSpacing = "editor-spacing";

        [TestMethod]
        public void ZoneCount()
        {
            ShortWait();
            OpenFancyZonesSettings();

            WindowsElement editorButton = WaitElementByXPath("//Button[@Name=\"Edit zones\"]");
            editorButton.Click();

            WindowsElement minusButton = WaitElementByAccessibilityId("decrementZones");
            WindowsElement zoneCount = WaitElementByAccessibilityId("zoneCount");
            WindowsElement applyButton;

            int editorZoneCountValue;
            Assert.IsTrue(Int32.TryParse(zoneCount.Text, out editorZoneCountValue));

            for (int i = editorZoneCountValue - 1, j = 0; i > -5; --i, ++j)
            {
                minusButton.Click();

                Assert.IsTrue(Int32.TryParse(zoneCount.Text, out editorZoneCountValue));
                Assert.AreEqual(Math.Max(i, 1), editorZoneCountValue);

                if (j == 0 || i == -4)
                {
                    applyButton = WaitElementByAccessibilityId("ApplyTemplateButton");
                    applyButton.Click();
                    ShortWait();
                    Assert.AreEqual(editorZoneCountValue, GetEditZonesSetting<int>(editorZoneCount));
                    editorButton.Click();
                    minusButton = WaitElementByAccessibilityId("decrementZones");
                    zoneCount = WaitElementByAccessibilityId("zoneCount");
                }
            }

            WindowsElement plusButton = WaitElementByAccessibilityId("incrementZones");

            for (int i = 2; i < 45; ++i)
            {
                plusButton.Click();

                Assert.IsTrue(Int32.TryParse(zoneCount.Text, out editorZoneCountValue));
                Assert.AreEqual(Math.Min(i, 40), editorZoneCountValue);
            }

            applyButton = WaitElementByAccessibilityId("ApplyTemplateButton");
            applyButton.Click();
            ShortWait();
            Assert.AreEqual(editorZoneCountValue, GetEditZonesSetting<int>(editorZoneCount));
        }

        [TestMethod]
        public void ShowSpacingTest()
        {
            ShortWait();
            OpenFancyZonesSettings();

            WindowsElement editorButton = WaitElementByXPath("//Button[@Name=\"Edit zones\"]");

            for (int i = 0; i < 2; ++i)
            {
                editorButton.Click();

                WindowsElement spaceAroundSetting = WaitElementByAccessibilityId("spaceAroundSetting", 20);
                bool spaceAroundSettingValue = spaceAroundSetting.Selected;
                WindowsElement applyButton = WaitElementByAccessibilityId("ApplyTemplateButton", 20);

                spaceAroundSetting.Click();
                applyButton.Click();

                ShortWait();

                Assert.AreNotEqual(spaceAroundSettingValue, GetEditZonesSetting<bool>(editorShowSpacing));
            }
        }

        [TestMethod]
        public void SpacingTestsValid()
        {
            ShortWait();
            OpenFancyZonesSettings();

            WindowsElement editorButton = WaitElementByXPath("//Button[@Name=\"Edit zones\"]");
            editorButton.Click();

            WindowsElement spaceAroundSetting = WaitElementByAccessibilityId("spaceAroundSetting");
            bool editorShowSpacingValue = spaceAroundSetting.Selected;

            string[] validValues = { "0", "1", "2", "3", "4", "5", "6", "7", "8", "9" };

            foreach (string editorSpacingValue in validValues)
            {
                editorButton.Click();

                WindowsElement paddingValue = WaitElementByAccessibilityId("paddingValue");
                ClearText(paddingValue);
                paddingValue.SendKeys(editorSpacingValue);

                WindowsElement applyButton = WaitElementByAccessibilityId("ApplyTemplateButton");
                applyButton.Click();
                ShortWait();

                Assert.AreEqual(editorShowSpacingValue, GetEditZonesSetting<bool>(editorShowSpacing));
                Assert.AreEqual(editorSpacingValue, GetEditZonesSetting<string>(editorSpacing));
            }
        }

        [TestMethod]
        public void SpacingTestsInvalid()
        {
            ShortWait();
            OpenFancyZonesSettings();

            WindowsElement editorButton = WaitElementByXPath("//Button[@Name=\"Edit zones\"]");
            editorButton.Click();

            WindowsElement spaceAroundSetting = WaitElementByAccessibilityId("spaceAroundSetting");
            bool editorShowSpacingValue = spaceAroundSetting.Selected;

            string[] invalidValues = { "!", "/", "<", "?", "D", "Z", "]", "m", "}", "1.5", "2,5" };

            string editorSpacingValue = GetEditZonesSetting<string>(editorSpacing);

            foreach (string value in invalidValues)
            {
                editorButton.Click();

                WindowsElement paddingValue = WaitElementByAccessibilityId("paddingValue");
                ClearText(paddingValue);
                paddingValue.SendKeys(value);

                WindowsElement applyButton = WaitElementByAccessibilityId("ApplyTemplateButton");
                applyButton.Click();
                ShortWait();

                Assert.AreEqual(editorShowSpacingValue, GetEditZonesSetting<bool>(editorShowSpacing));
                Assert.AreEqual(editorSpacingValue, GetEditZonesSetting<string>(editorSpacing));
            }
        }

        [TestMethod]
        public void SpacingTestLargeValue()
        {
            string zoneSettings = File.ReadAllText(_zoneSettingsPath);

            const string largeValue = "1000";

            ShortWait();
            OpenFancyZonesSettings();

            WindowsElement editorButton = WaitElementByXPath("//Button[@Name=\"Edit zones\"]");
            editorButton.Click();

            WindowsElement paddingValue = WaitElementByAccessibilityId("paddingValue");
            ClearText(paddingValue);
            ClearText(paddingValue);
            paddingValue.SendKeys(largeValue);

            WindowsElement applyButton = WaitElementByAccessibilityId("ApplyTemplateButton");
            applyButton.Click();

            editorButton.Click();
            WindowsElement editorWindow = null;
            try
            {
                editorWindow = WaitElementByAccessibilityId("MainWindow1");
            }
            catch { }

            bool result = editorWindow == null;

            if (editorWindow != null)
            {
                editorWindow.SendKeys(Keys.Alt + Keys.F4);
            }

            ExitPowerToys();
            File.WriteAllText(_zoneSettingsPath, zoneSettings);
            LaunchPowerToys();
            OpenSettings();

            Assert.IsFalse(result);
        }

        private T GetEditZonesSetting<T>(string value)
        {
            JObject zoneSettings = JObject.Parse(File.ReadAllText(_zoneSettingsPath));
            T result = zoneSettings["devices"][0][value].ToObject<T>();
            return result;
        }

        private void ClearText(WindowsElement windowsElement)
        {
            windowsElement.SendKeys(Keys.Home);
            windowsElement.SendKeys(Keys.Control + Keys.Delete);
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