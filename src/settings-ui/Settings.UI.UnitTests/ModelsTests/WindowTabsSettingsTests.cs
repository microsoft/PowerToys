// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CommonLibTest
{
    [TestClass]
    public class WindowTabsSettingsTests
    {
        [TestMethod]
        public void DefaultsAgreeWithNativeModule()
        {
            var settings = new WindowTabsSettings();
            Assert.AreEqual("WindowTabs", settings.Name);
            Assert.AreEqual("1.0", settings.Version);
            Assert.IsFalse(new EnabledModules().WindowTabs);
            Assert.IsTrue(settings.Properties.RestoreSession.Value);
            Assert.IsTrue(settings.Properties.NextTabShortcut.Win);
            Assert.IsTrue(settings.Properties.NextTabShortcut.Ctrl);
            Assert.AreEqual(0x22, settings.Properties.NextTabShortcut.Code);
            Assert.AreEqual(0x21, settings.Properties.PreviousTabShortcut.Code);
        }

        [TestMethod]
        public void AotSerializationPreservesNativePropertyNamesAndValues()
        {
            var settings = new WindowTabsSettings();
            settings.Properties.RestoreSession.Value = false;
            settings.Properties.NextTabShortcut = new HotkeySettings(true, false, true, false, 0x4E);
            var json = settings.ToJsonString();
            using var document = JsonDocument.Parse(json);
            var properties = document.RootElement.GetProperty("properties");
            Assert.IsFalse(properties.GetProperty("restore_session").GetProperty("value").GetBoolean());
            Assert.AreEqual(0x4E, properties.GetProperty("next_tab_shortcut").GetProperty("code").GetInt32());
            Assert.IsTrue(properties.TryGetProperty("previous_tab_shortcut", out _));
            var restored = JsonSerializer.Deserialize(json, SettingsSerializationContext.Default.WindowTabsSettings);
            Assert.IsNotNull(restored);
            Assert.IsFalse(restored.Properties.RestoreSession.Value);
            Assert.IsTrue(restored.Properties.NextTabShortcut.Alt);
        }

        [TestMethod]
        public void MissingSettingsAndNullShortcutsUseDefaults()
        {
            var settings = JsonSerializer.Deserialize<WindowTabsSettings>("{\"properties\":{\"next_tab_shortcut\":null}}");
            Assert.IsNotNull(settings);
            Assert.AreEqual(0x22, settings.Properties.NextTabShortcut.Code);
            Assert.IsTrue(settings.Properties.RestoreSession.Value);
        }
    }
}
