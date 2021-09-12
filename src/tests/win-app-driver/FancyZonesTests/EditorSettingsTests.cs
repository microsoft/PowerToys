using System;
using System.IO.Abstractions;
//using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;

namespace PowerToysTests
{
    [TestClass]
    public class FancyZonesEditorSettingsTests : FancyZonesEditor
    {
        private static readonly IFileSystem FileSystem = new FileSystem();
        private static readonly IFile File = FileSystem.File;

        private const string editorZoneCount = "editor-zone-count";
        private const string editorShowSpacing = "editor-show-spacing";
        private const string editorSpacing = "editor-spacing";

        [TestMethod]
        public void ZoneCount()
        {
            Assert.IsTrue(OpenEditor());

            AppiumWebElement minusButton = editorWindow.FindElementByAccessibilityId("decrementZones");
            AppiumWebElement zoneCount = editorWindow.FindElementByAccessibilityId("zoneCount");

            int editorZoneCountValue;
            Assert.IsTrue(Int32.TryParse(zoneCount.Text, out editorZoneCountValue));

            for (int i = editorZoneCountValue - 1, j = 0; i > -5; --i, ++j)
            {
                minusButton.Click();

                Assert.IsTrue(Int32.TryParse(zoneCount.Text, out editorZoneCountValue));
                Assert.AreEqual(Math.Max(i, 1), editorZoneCountValue);

                if (j == 0 || i == -4)
                {
                    editorWindow.FindElementByAccessibilityId("ApplyTemplateButton").Click();

                    WaitSeconds(1);
                    Assert.AreEqual(editorZoneCountValue, GetEditZonesSetting<int>(editorZoneCount));
                    Assert.IsTrue(OpenEditor());

                    minusButton = editorWindow.FindElementByAccessibilityId("decrementZones");
                    zoneCount = editorWindow.FindElementByAccessibilityId("zoneCount");
                }
            }

            AppiumWebElement plusButton = editorWindow.FindElementByAccessibilityId("incrementZones");

            for (int i = 2; i < 45; ++i)
            {
                plusButton.Click();

                Assert.IsTrue(Int32.TryParse(zoneCount.Text, out editorZoneCountValue));
                Assert.AreEqual(Math.Min(i, 40), editorZoneCountValue);
            }

            editorWindow.FindElementByAccessibilityId("ApplyTemplateButton").Click();
            WaitSeconds(1);
            Assert.AreEqual(editorZoneCountValue, GetEditZonesSetting<int>(editorZoneCount));
        }

        [TestMethod]
        public void ShowSpacingTest()
        {
            for (int i = 0; i < 2; ++i)
            {
                Assert.IsTrue(OpenEditor());

                AppiumWebElement spaceAroundSetting = editorWindow.FindElementByAccessibilityId("spaceAroundSetting");
                bool spaceAroundSettingValue = spaceAroundSetting.Selected;
                spaceAroundSetting.Click();

                editorWindow.FindElementByAccessibilityId("ApplyTemplateButton").Click();

                WaitSeconds(1);

                Assert.AreNotEqual(spaceAroundSettingValue, GetEditZonesSetting<bool>(editorShowSpacing));
            }
        }

        [TestMethod]
        public void SpacingTestsValid()
        {
            Assert.IsTrue(OpenEditor());

            AppiumWebElement spaceAroundSetting = editorWindow.FindElementByAccessibilityId("spaceAroundSetting");
            bool editorShowSpacingValue = spaceAroundSetting.Selected;

            editorWindow.FindElementByAccessibilityId("ApplyTemplateButton").Click();

            string[] validValues = { "0", "1", "2", "3", "4", "5", "6", "7", "8", "9" };

            foreach (string editorSpacingValue in validValues)
            {
                Assert.IsTrue(OpenEditor());

                AppiumWebElement paddingValue = editorWindow.FindElementByAccessibilityId("paddingValue");
                ClearText(paddingValue);
                paddingValue.SendKeys(editorSpacingValue);

                editorWindow.FindElementByAccessibilityId("ApplyTemplateButton").Click();
                WaitSeconds(1);

                Assert.AreEqual(editorShowSpacingValue, GetEditZonesSetting<bool>(editorShowSpacing));
                Assert.AreEqual(editorSpacingValue, GetEditZonesSetting<string>(editorSpacing));
            }
        }

        [TestMethod]
        public void SpacingTestsInvalid()
        {
            Assert.IsTrue(OpenEditor());

            AppiumWebElement spaceAroundSetting = editorWindow.FindElementByAccessibilityId("spaceAroundSetting");
            bool editorShowSpacingValue = spaceAroundSetting.Selected;

            editorWindow.FindElementByAccessibilityId("ApplyTemplateButton").Click();

            string[] invalidValues = { "!", "/", "<", "?", "D", "Z", "]", "m", "}", "1.5", "2,5" };

            string editorSpacingValue = GetEditZonesSetting<string>(editorSpacing);

            foreach (string value in invalidValues)
            {
                Assert.IsTrue(OpenEditor());

                AppiumWebElement paddingValue = editorWindow.FindElementByAccessibilityId("paddingValue");
                ClearText(paddingValue);
                paddingValue.SendKeys(value);

                editorWindow.FindElementByAccessibilityId("ApplyTemplateButton").Click();

                Assert.AreEqual(editorShowSpacingValue, GetEditZonesSetting<bool>(editorShowSpacing));
                Assert.AreEqual(editorSpacingValue, GetEditZonesSetting<string>(editorSpacing));
            }
        }

        [TestMethod]
        public void SpacingTestLargeValue()
        {
            Assert.IsTrue(OpenEditor());
            editorWindow.FindElementByName("Grid").Click();

            AppiumWebElement paddingValue = editorWindow.FindElementByAccessibilityId("paddingValue");
            ClearText(paddingValue);
            paddingValue.SendKeys("1000");

            editorWindow.FindElementByAccessibilityId("ApplyTemplateButton").Click();
            editorWindow = null;

            try
            {
                Assert.IsTrue(OpenEditor());
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

        private void ClearText(AppiumWebElement windowsElement)
        {
            windowsElement.SendKeys(Keys.Home);
            windowsElement.SendKeys(Keys.Control + Keys.Delete);
        }

        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            Setup(context);
            Assert.IsNotNull(session);
            EnableModules(false, true, false, false, false, false, false, false);
            ResetSettings();
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
            CloseSettings();
            ResetDefaultFancyZonesSettings(false);
            ResetDefaultZoneSettings(false);
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