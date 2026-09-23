// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CommonLibTest
{
    [TestClass]
    public class AutoHideCursorSettingsTests
    {
        [TestMethod]
        public void DefaultsHideOnTypingOnly()
        {
            var settings = new AutoHideCursorSettings();

            Assert.IsTrue(settings.Properties.HideOnTyping.Value);
            Assert.IsFalse(settings.Properties.HideOnIdle.Value);
            Assert.AreEqual(AutoHideCursorProperties.DefaultIdleDelayMs, settings.Properties.IdleDelayMs.Value);
        }

        [TestMethod]
        public void SettingsRoundTripPreservesIndependentTriggers()
        {
            var settings = new AutoHideCursorSettings();
            settings.Properties.HideOnTyping.Value = false;
            settings.Properties.HideOnIdle.Value = true;
            settings.Properties.IdleDelayMs.Value = 12000;

            var deserialized = JsonSerializer.Deserialize<AutoHideCursorSettings>(settings.ToJsonString());

            Assert.IsNotNull(deserialized);
            Assert.IsFalse(deserialized.Properties.HideOnTyping.Value);
            Assert.IsTrue(deserialized.Properties.HideOnIdle.Value);
            Assert.AreEqual(12000, deserialized.Properties.IdleDelayMs.Value);
        }

        [TestMethod]
        [DataRow(0, AutoHideCursorProperties.MinimumIdleDelayMs)]
        [DataRow(1000, 1000)]
        [DataRow(60000, 60000)]
        [DataRow(90000, AutoHideCursorProperties.MaximumIdleDelayMs)]
        public void UpgradeClampsIdleDelay(int configuredValue, int expectedValue)
        {
            var settings = new AutoHideCursorSettings();
            settings.Properties.IdleDelayMs.Value = configuredValue;

            settings.UpgradeSettingsConfiguration();

            Assert.AreEqual(expectedValue, settings.Properties.IdleDelayMs.Value);
        }
    }
}
