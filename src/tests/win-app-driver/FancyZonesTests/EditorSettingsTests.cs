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
    public class FancyZonesEditorSettingsTests : FancyZonesEditor
    {
        private const string editorZoneCount = "editor-zone-count";
        private const string editorShowSpacing = "editor-show-spacing";
        private const string editorSpacing = "editor-spacing";

        [TestMethod]
        public void ZoneCount()
        {
            OpenEditor();

            WindowsElement minusButton = session.FindElementByAccessibilityId("decrementZones");
            WindowsElement zoneCount = session.FindElementByAccessibilityId("zoneCount");

            int editorZoneCountValue;
            Assert.IsTrue(Int32.TryParse(zoneCount.Text, out editorZoneCountValue));

            for (int i = editorZoneCountValue - 1, j = 0; i > -5; --i, ++j)
            {
                minusButton.Click();

                Assert.IsTrue(Int32.TryParse(zoneCount.Text, out editorZoneCountValue));
                Assert.AreEqual(Math.Max(i, 1), editorZoneCountValue);

                if (j == 0 || i == -4)
                {
                    session.FindElementByAccessibilityId("ApplyTemplateButton").Click();

                    WaitSeconds(1);
                    Assert.AreEqual(editorZoneCountValue, GetEditZonesSetting<int>(editorZoneCount));
                    OpenEditor();
                    
                    minusButton = session.FindElementByAccessibilityId("decrementZones");
                    zoneCount = session.FindElementByAccessibilityId("zoneCount");
                }
            }

            WindowsElement plusButton = session.FindElementByAccessibilityId("incrementZones");

            for (int i = 2; i < 45; ++i)
            {
                plusButton.Click();

                Assert.IsTrue(Int32.TryParse(zoneCount.Text, out editorZoneCountValue));
                Assert.AreEqual(Math.Min(i, 40), editorZoneCountValue);
            }

            session.FindElementByAccessibilityId("ApplyTemplateButton").Click();
            WaitSeconds(1);
            Assert.AreEqual(editorZoneCountValue, GetEditZonesSetting<int>(editorZoneCount));
        }

        [TestMethod]
        public void ShowSpacingTest()
        {
           for (int i = 0; i < 2; ++i)
            {
                OpenEditor();

                WindowsElement spaceAroundSetting = session.FindElementByAccessibilityId("spaceAroundSetting");
                bool spaceAroundSettingValue = spaceAroundSetting.Selected;
                spaceAroundSetting.Click();

                session.FindElementByAccessibilityId("ApplyTemplateButton").Click();

                WaitSeconds(1);

                Assert.AreNotEqual(spaceAroundSettingValue, GetEditZonesSetting<bool>(editorShowSpacing));
            }
        }

        [TestMethod]
        public void SpacingTestsValid()
        {
            OpenEditor();

            WindowsElement spaceAroundSetting = session.FindElementByAccessibilityId("spaceAroundSetting");
            bool editorShowSpacingValue = spaceAroundSetting.Selected;

            session.FindElementByAccessibilityId("ApplyTemplateButton").Click();
            WaitSeconds(1);

            string[] validValues = { "0", "1", "2", "3", "4", "5", "6", "7", "8", "9" };

            foreach (string editorSpacingValue in validValues)
            {
                OpenEditor();

                WindowsElement paddingValue = WaitElementByAccessibilityId("paddingValue");
                ClearText(paddingValue);
                paddingValue.SendKeys(editorSpacingValue);

                session.FindElementByAccessibilityId("ApplyTemplateButton").Click();
                WaitSeconds(1);

                Assert.AreEqual(editorShowSpacingValue, GetEditZonesSetting<bool>(editorShowSpacing));
                Assert.AreEqual(editorSpacingValue, GetEditZonesSetting<string>(editorSpacing));
            }
        }

        [TestMethod]
        public void SpacingTestsInvalid()
        {
            OpenEditor();

            WindowsElement spaceAroundSetting = session.FindElementByAccessibilityId("spaceAroundSetting");
            bool editorShowSpacingValue = spaceAroundSetting.Selected;

            session.FindElementByAccessibilityId("ApplyTemplateButton").Click();
            WaitSeconds(1);

            string[] invalidValues = { "!", "/", "<", "?", "D", "Z", "]", "m", "}", "1.5", "2,5" };

            string editorSpacingValue = GetEditZonesSetting<string>(editorSpacing);

            foreach (string value in invalidValues)
            {
                OpenEditor();

                WindowsElement paddingValue = WaitElementByAccessibilityId("paddingValue");
                ClearText(paddingValue);
                paddingValue.SendKeys(value);

                session.FindElementByAccessibilityId("ApplyTemplateButton").Click();
                WaitSeconds(1);

                Assert.AreEqual(editorShowSpacingValue, GetEditZonesSetting<bool>(editorShowSpacing));
                Assert.AreEqual(editorSpacingValue, GetEditZonesSetting<string>(editorSpacing));
            }
        }
        
        [TestMethod]
        public void SpacingTestLargeValue()
        {
            OpenEditor();
            session.FindElementByXPath("//Text[@Name=\"Grid\"]").Click();

            WindowsElement paddingValue = session.FindElementByAccessibilityId("paddingValue");
            ClearText(paddingValue);
            paddingValue.SendKeys("1000");

            session.FindElementByAccessibilityId("ApplyTemplateButton").Click();

            editorWindow = null;
            try
            {
                OpenEditor();
            }
            catch { }

            Assert.AreNotEqual(editorWindow, null, "Editor Zones Window is not starting after setting large padding value");
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
            Setup(context, false);
            ResetSettings();
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
            ResetSettings();
        }
    }
}