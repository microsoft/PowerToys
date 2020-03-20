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

        private int getSavedZoneCount()
        {
            JObject zoneSettings = JObject.Parse(File.ReadAllText(_zoneSettingsPath));
            int editorZoneCount = (int)zoneSettings["devices"][0]["editor-zone-count"];
            return editorZoneCount;
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