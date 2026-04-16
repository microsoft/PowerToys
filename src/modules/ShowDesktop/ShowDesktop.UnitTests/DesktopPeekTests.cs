// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ShowDesktop.UnitTests
{
    [TestClass]
    public class DesktopPeekTests
    {
        private static ShowDesktopSettings CreateSettings(
            int peekMode = 0,
            bool requireDoubleClick = false,
            bool enableTaskbarPeek = false,
            bool enableGamingDetection = true)
        {
            var settings = new ShowDesktopSettings();
            settings.Properties.PeekMode = new IntProperty(peekMode);
            settings.Properties.RequireDoubleClick = new BoolProperty(requireDoubleClick);
            settings.Properties.EnableTaskbarPeek = new BoolProperty(enableTaskbarPeek);
            settings.Properties.EnableGamingDetection = new BoolProperty(enableGamingDetection);
            return settings;
        }

        [TestMethod]
        public void Constructor_WithDefaultSettings_DoesNotThrow()
        {
            var settings = CreateSettings();
            using var peek = new DesktopPeek(settings);

            // No exception means construction succeeded
            Assert.IsNotNull(peek);
        }

        [TestMethod]
        public void Constructor_WithNativeMode_DoesNotThrow()
        {
            var settings = CreateSettings(peekMode: (int)PeekMode.Native);
            using var peek = new DesktopPeek(settings);
            Assert.IsNotNull(peek);
        }

        [TestMethod]
        public void Constructor_WithMinimizeMode_DoesNotThrow()
        {
            var settings = CreateSettings(peekMode: (int)PeekMode.Minimize);
            using var peek = new DesktopPeek(settings);
            Assert.IsNotNull(peek);
        }

        [TestMethod]
        public void Constructor_WithFlyAwayMode_DoesNotThrow()
        {
            var settings = CreateSettings(peekMode: (int)PeekMode.FlyAway);
            using var peek = new DesktopPeek(settings);
            Assert.IsNotNull(peek);
        }

        [TestMethod]
        public void UpdateSettings_AppliesNewSettings()
        {
            var settings = CreateSettings(peekMode: (int)PeekMode.Native);
            using var peek = new DesktopPeek(settings);

            var newSettings = CreateSettings(
                peekMode: (int)PeekMode.Minimize,
                requireDoubleClick: true,
                enableTaskbarPeek: true,
                enableGamingDetection: false);

            // Should not throw
            peek.UpdateSettings(newSettings);
        }

        [TestMethod]
        public void Dispose_CalledTwice_DoesNotThrow()
        {
            var settings = CreateSettings();
            var peek = new DesktopPeek(settings);
            peek.Dispose();
            peek.Dispose(); // second dispose should be safe
        }

        [TestMethod]
        public void Constructor_WithDoubleClickEnabled_DoesNotThrow()
        {
            var settings = CreateSettings(requireDoubleClick: true);
            using var peek = new DesktopPeek(settings);
            Assert.IsNotNull(peek);
        }

        [TestMethod]
        public void Constructor_WithTaskbarPeekEnabled_DoesNotThrow()
        {
            var settings = CreateSettings(enableTaskbarPeek: true);
            using var peek = new DesktopPeek(settings);
            Assert.IsNotNull(peek);
        }

        [TestMethod]
        public void Constructor_WithGamingDetectionDisabled_DoesNotThrow()
        {
            var settings = CreateSettings(enableGamingDetection: false);
            using var peek = new DesktopPeek(settings);
            Assert.IsNotNull(peek);
        }

        [TestMethod]
        public void Constructor_AllSettingsCombined_DoesNotThrow()
        {
            var settings = CreateSettings(
                peekMode: (int)PeekMode.FlyAway,
                requireDoubleClick: true,
                enableTaskbarPeek: true,
                enableGamingDetection: false);
            using var peek = new DesktopPeek(settings);
            Assert.IsNotNull(peek);
        }
    }
}
