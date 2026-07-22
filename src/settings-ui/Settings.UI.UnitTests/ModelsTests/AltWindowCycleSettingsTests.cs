// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;

using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CommonLibTest
{
    [TestClass]
    public class AltWindowCycleSettingsTests
    {
        // VK_OEM_3 (the backtick / grave-accent key) that both default shortcuts bind to.
        private const int OemTilde = 0xC0;

        [TestMethod]
        public void Defaults_ShouldMatchDocumentedHotkeys()
        {
            var settings = new AltWindowCycleSettings();

            Assert.AreEqual(AltWindowCycleSettings.ModuleName, settings.Name);
            Assert.AreEqual("1.0", settings.Version);
            Assert.IsNotNull(settings.Properties);

            // Next window: Alt+`
            AssertHotkey(settings.Properties.NextWindowShortcut, win: false, ctrl: false, alt: true, shift: false, code: OemTilde);

            // Previous window: Shift+Alt+`
            AssertHotkey(settings.Properties.PreviousWindowShortcut, win: false, ctrl: false, alt: true, shift: true, code: OemTilde);
        }

        [TestMethod]
        public void ToJsonString_ShouldContainExpectedKeys()
        {
            var settings = new AltWindowCycleSettings();

            var json = settings.ToJsonString();

            StringAssert.Contains(json, AltWindowCycleSettings.ModuleName);
            StringAssert.Contains(json, "\"properties\"");
            StringAssert.Contains(json, "next_window_shortcut");
            StringAssert.Contains(json, "previous_window_shortcut");
        }

        [TestMethod]
        public void RoundTrip_WithDefaults_ShouldPreserveAllValues()
        {
            var original = new AltWindowCycleSettings();

            var deserialized = JsonSerializer.Deserialize<AltWindowCycleSettings>(original.ToJsonString());

            Assert.IsNotNull(deserialized);
            Assert.AreEqual(original.Name, deserialized.Name);
            Assert.AreEqual(original.Version, deserialized.Version);
            Assert.IsNotNull(deserialized.Properties);
            AssertHotkeyEqual(original.Properties.NextWindowShortcut, deserialized.Properties.NextWindowShortcut);
            AssertHotkeyEqual(original.Properties.PreviousWindowShortcut, deserialized.Properties.PreviousWindowShortcut);
        }

        [TestMethod]
        public void RoundTrip_WithCustomHotkeys_ShouldPreserveOverrides()
        {
            var original = new AltWindowCycleSettings();
            original.Properties.NextWindowShortcut = new HotkeySettings(true, false, false, false, 0x4E); // Win+N
            original.Properties.PreviousWindowShortcut = new HotkeySettings(true, false, false, true, 0x50); // Win+Shift+P

            var deserialized = JsonSerializer.Deserialize<AltWindowCycleSettings>(original.ToJsonString());

            Assert.IsNotNull(deserialized);
            AssertHotkey(deserialized.Properties.NextWindowShortcut, win: true, ctrl: false, alt: false, shift: false, code: 0x4E);
            AssertHotkey(deserialized.Properties.PreviousWindowShortcut, win: true, ctrl: false, alt: false, shift: true, code: 0x50);
        }

        [TestMethod]
        public void ShouldBeRegisteredInSerializationContext()
        {
            var options = new JsonSerializerOptions
            {
                TypeInfoResolver = SettingsSerializationContext.Default,
            };

            var typeInfo = options.TypeInfoResolver?.GetTypeInfo(typeof(AltWindowCycleSettings), options);

            Assert.IsNotNull(typeInfo, "AltWindowCycleSettings must be registered in SettingsSerializationContext for Native AOT serialization.");
        }

        private static void AssertHotkey(HotkeySettings hotkey, bool win, bool ctrl, bool alt, bool shift, int code)
        {
            Assert.IsNotNull(hotkey);
            Assert.AreEqual(win, hotkey.Win, "Win modifier mismatch.");
            Assert.AreEqual(ctrl, hotkey.Ctrl, "Ctrl modifier mismatch.");
            Assert.AreEqual(alt, hotkey.Alt, "Alt modifier mismatch.");
            Assert.AreEqual(shift, hotkey.Shift, "Shift modifier mismatch.");
            Assert.AreEqual(code, hotkey.Code, "Key code mismatch.");
        }

        private static void AssertHotkeyEqual(HotkeySettings expected, HotkeySettings actual)
        {
            AssertHotkey(actual, expected.Win, expected.Ctrl, expected.Alt, expected.Shift, expected.Code);
        }
    }
}
