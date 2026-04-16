// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ShowDesktop.UnitTests
{
    [TestClass]
    public class ShowDesktopSettingsTests
    {
        [TestMethod]
        public void DefaultSettings_PeekMode_IsNative()
        {
            var settings = new ShowDesktopSettings();
            Assert.AreEqual(ShowDesktopProperties.DefaultPeekMode, settings.Properties.PeekMode.Value);
            Assert.AreEqual(0, settings.Properties.PeekMode.Value);
        }

        [TestMethod]
        public void DefaultSettings_RequireDoubleClick_IsFalse()
        {
            var settings = new ShowDesktopSettings();
            Assert.AreEqual(ShowDesktopProperties.DefaultRequireDoubleClick, settings.Properties.RequireDoubleClick.Value);
            Assert.IsFalse(settings.Properties.RequireDoubleClick.Value);
        }

        [TestMethod]
        public void DefaultSettings_EnableTaskbarPeek_IsFalse()
        {
            var settings = new ShowDesktopSettings();
            Assert.AreEqual(ShowDesktopProperties.DefaultEnableTaskbarPeek, settings.Properties.EnableTaskbarPeek.Value);
            Assert.IsFalse(settings.Properties.EnableTaskbarPeek.Value);
        }

        [TestMethod]
        public void DefaultSettings_EnableGamingDetection_IsTrue()
        {
            var settings = new ShowDesktopSettings();
            Assert.AreEqual(ShowDesktopProperties.DefaultEnableGamingDetection, settings.Properties.EnableGamingDetection.Value);
            Assert.IsTrue(settings.Properties.EnableGamingDetection.Value);
        }

        [TestMethod]
        public void DefaultSettings_FlyAwayDuration_Is300()
        {
            var settings = new ShowDesktopSettings();
            Assert.AreEqual(ShowDesktopProperties.DefaultFlyAwayAnimationDurationMs, settings.Properties.FlyAwayAnimationDurationMs.Value);
            Assert.AreEqual(300, settings.Properties.FlyAwayAnimationDurationMs.Value);
        }

        [TestMethod]
        public void GetModuleName_ReturnsShowDesktop()
        {
            var settings = new ShowDesktopSettings();
            Assert.AreEqual("ShowDesktop", settings.GetModuleName());
        }

        [TestMethod]
        public void ModuleName_Constant_IsShowDesktop()
        {
            Assert.AreEqual("ShowDesktop", ShowDesktopSettings.ModuleName);
        }

        [TestMethod]
        public void Properties_PeekMode_CanBeSet()
        {
            var settings = new ShowDesktopSettings();
            settings.Properties.PeekMode = new IntProperty((int)PeekMode.Minimize);
            Assert.AreEqual((int)PeekMode.Minimize, settings.Properties.PeekMode.Value);
        }

        [TestMethod]
        public void Properties_ToJsonString_IsNotEmpty()
        {
            var properties = new ShowDesktopProperties();
            string json = properties.ToJsonString();
            Assert.IsFalse(string.IsNullOrEmpty(json));
        }

        [TestMethod]
        public void Properties_ToJsonString_ContainsPeekMode()
        {
            var properties = new ShowDesktopProperties();
            string json = properties.ToJsonString();
            Assert.IsTrue(json.Contains("peek-mode"));
        }

        [TestMethod]
        public void Properties_ToJsonString_ContainsAllProperties()
        {
            var properties = new ShowDesktopProperties();
            string json = properties.ToJsonString();
            Assert.IsTrue(json.Contains("peek-mode"));
            Assert.IsTrue(json.Contains("require-double-click"));
            Assert.IsTrue(json.Contains("enable-taskbar-peek"));
            Assert.IsTrue(json.Contains("enable-gaming-detection"));
            Assert.IsTrue(json.Contains("fly-away-animation-duration-ms"));
        }

        [TestMethod]
        public void UpgradeSettingsConfiguration_ReturnsFalse()
        {
            var settings = new ShowDesktopSettings();
            Assert.IsFalse(settings.UpgradeSettingsConfiguration());
        }
    }
}
