using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium.Windows;

namespace PowerToysTests
{
    [TestClass]
    public class FancyZonesEditorSettingsTests : PowerToysSession
    {
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

            int zoneCountQty;
            Assert.IsTrue(Int32.TryParse(zoneCount.Text, out zoneCountQty));

            for (int i = zoneCountQty - 1, j = 0; i > -5; --i, ++j)
            {
                minusButton.Click();

                Assert.IsTrue(Int32.TryParse(zoneCount.Text, out zoneCountQty));
                Assert.AreEqual(Math.Max(i, 1), zoneCountQty);

                if (j == 0 || i == -4)
                {
                    applyButton = WaitElementByAccessibilityId("ApplyTemplateButton");
                    applyButton.Click();
                    ShortWait();
                    Assert.AreEqual(zoneCountQty, getSavedZoneCount());
                    editorButton.Click();
                    minusButton = WaitElementByAccessibilityId("decrementZones");
                    zoneCount = WaitElementByAccessibilityId("zoneCount");
                }
            }

            WindowsElement plusButton = WaitElementByAccessibilityId("incrementZones");

            for (int i = 2; i < 45; ++i)
            {
                plusButton.Click();

                Assert.IsTrue(Int32.TryParse(zoneCount.Text, out zoneCountQty));
                Assert.AreEqual(Math.Min(i, 40), zoneCountQty);
            }

            applyButton = WaitElementByAccessibilityId("ApplyTemplateButton");
            applyButton.Click();
            ShortWait();
            Assert.AreEqual(zoneCountQty, getSavedZoneCount());
        }

        [TestMethod]
        public void SpacingTestsValid()
        {
            ShortWait();
            OpenFancyZonesSettings();

            WindowsElement editorButton = WaitElementByXPath("//Button[@Name=\"Edit zones\"]");
            editorButton.Click();

            WindowsElement spaceAroundSetting = WaitElementByAccessibilityId("spaceAroundSetting");
            bool spaceAroundSettingValue = spaceAroundSetting.Selected;

            string[] validValues = { "1", "2", "3", "4", "5", "6", "7", "8", "9" };

            foreach (string value in validValues)
            {
                WindowsElement paddingValue = WaitElementByAccessibilityId("paddingValue");
                clearText(paddingValue);
                paddingValue.SendKeys(value);
                WindowsElement applyButton = WaitElementByAccessibilityId("ApplyTemplateButton");
                applyButton.Click();
                ShortWait();
                Assert.AreEqual(spaceAroundSettingValue, getSavedSpaceAroundSetting());
                Assert.AreEqual(value, getPaddingValue());
                editorButton.Click();
            }

            spaceAroundSetting = WaitElementByAccessibilityId("spaceAroundSetting");
            spaceAroundSetting.Click();
            spaceAroundSettingValue = spaceAroundSetting.Selected;

            foreach (string value in validValues)
            {
                WindowsElement paddingValue = WaitElementByAccessibilityId("paddingValue");
                clearText(paddingValue);
                paddingValue.SendKeys(value);
                WindowsElement applyButton = WaitElementByAccessibilityId("ApplyTemplateButton");
                applyButton.Click();
                ShortWait();
                Assert.AreEqual(spaceAroundSettingValue, getSavedSpaceAroundSetting());
                Assert.AreEqual(value, getPaddingValue());
                editorButton.Click();
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
            bool spaceAroundSettingValue = spaceAroundSetting.Selected;

            string[] invalidValues = { "!", "/", "<", "?", "D", "Z", "]", "m", "}" };

            string savedPadding = getPaddingValue();

            foreach (string value in invalidValues)
            {
                WindowsElement paddingValue = WaitElementByAccessibilityId("paddingValue");
                clearText(paddingValue);
                paddingValue.SendKeys(value);
                WindowsElement applyButton = WaitElementByAccessibilityId("ApplyTemplateButton");
                applyButton.Click();
                ShortWait();
                Assert.AreEqual(spaceAroundSettingValue, getSavedSpaceAroundSetting());
                Assert.AreEqual(savedPadding, getPaddingValue());
                editorButton.Click();
            }

            spaceAroundSetting = WaitElementByAccessibilityId("spaceAroundSetting");
            spaceAroundSetting.Click();
            spaceAroundSettingValue = spaceAroundSetting.Selected;

            foreach (string value in invalidValues)
            {
                WindowsElement paddingValue = WaitElementByAccessibilityId("paddingValue");
                clearText(paddingValue);
                paddingValue.SendKeys(value);
                WindowsElement applyButton = WaitElementByAccessibilityId("ApplyTemplateButton");
                applyButton.Click();
                ShortWait();
                Assert.AreEqual(spaceAroundSettingValue, getSavedSpaceAroundSetting());
                Assert.AreEqual(savedPadding, getPaddingValue());
                editorButton.Click();
            }
        }

        private int getSavedZoneCount()
        {
            JObject zoneSettings = JObject.Parse(File.ReadAllText(_zoneSettingsPath));
            int editorZoneCount = (int)zoneSettings["devices"][0]["editor-zone-count"];
            return editorZoneCount;
        }

        private bool getSavedSpaceAroundSetting()
        {
            JObject zoneSettings = JObject.Parse(File.ReadAllText(_zoneSettingsPath));
            bool editorSpaceAroundSetting = (bool)zoneSettings["devices"][0]["editor-show-spacing"];
            return editorSpaceAroundSetting;
        }

        private string getPaddingValue()
        {
            JObject zoneSettings = JObject.Parse(File.ReadAllText(_zoneSettingsPath));
            string editorSpaceAroundSetting = (string)zoneSettings["devices"][0]["editor-spacing"];
            return editorSpaceAroundSetting;
        }

        private void clearText(WindowsElement windowsElement)
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