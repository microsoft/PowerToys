// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Settings.UI.UnitTests
{
    // The settings of the module, and the file that they are kept in.
    [TestClass]
    public class SoundManagerSettingsTests
    {
        private const string FirefoxPath = "C:\\Program Files\\Mozilla Firefox\\firefox.exe";

        [TestMethod]
        public void TheModuleIsCalledSoundManager()
        {
            Assert.AreEqual("Sound Manager", SoundManagerSettings.ModuleName);
        }

        [TestMethod]
        public void NewSettingsHaveTheDefaultShortcutAndNoMuteShortcuts()
        {
            var settings = new SoundManagerSettings();

            Assert.IsTrue(settings.Properties.ActivationShortcut.Win);
            Assert.IsTrue(settings.Properties.ActivationShortcut.Ctrl);
            Assert.IsTrue(settings.Properties.ActivationShortcut.Shift);
            Assert.IsFalse(settings.Properties.ActivationShortcut.Alt);
            Assert.AreEqual(0x56, settings.Properties.ActivationShortcut.Code);
            Assert.AreEqual(0, settings.Properties.MuteShortcuts.Value.Count);
        }

        [TestMethod]
        public void TheMuteShortcutsSurviveTheWritingAndTheReadingOfTheSettings()
        {
            var settings = new SoundManagerSettings();
            settings.Properties.MuteShortcuts.Value.Add(new SoundManagerMuteShortcut
            {
                Program = "firefox.exe",
                Name = "firefox",
                ProcessPath = FirefoxPath,
                Shortcut = new HotkeySettings(true, true, false, true, 0x46),
            });

            var read = Read(settings.ToJsonString());

            Assert.AreEqual(1, read.Properties.MuteShortcuts.Value.Count);

            var shortcut = read.Properties.MuteShortcuts.Value[0];
            Assert.AreEqual("firefox.exe", shortcut.Program);
            Assert.AreEqual("firefox", shortcut.Name);
            Assert.AreEqual(FirefoxPath, shortcut.ProcessPath);
            Assert.IsTrue(shortcut.Shortcut.Win);
            Assert.IsTrue(shortcut.Shortcut.Ctrl);
            Assert.IsTrue(shortcut.Shortcut.Shift);
            Assert.IsFalse(shortcut.Shortcut.Alt);
            Assert.AreEqual(0x46, shortcut.Shortcut.Code);
        }

        [TestMethod]
        public void TheShortcutOfTheWindowComesBeforeTheShortcutsOfThePrograms()
        {
            var settings = new SoundManagerSettings();
            settings.Properties.MuteShortcuts.Value.Add(new SoundManagerMuteShortcut
            {
                Program = "firefox.exe",
                Name = "firefox",
                Shortcut = new HotkeySettings(true, true, false, true, 0x46),
            });

            var accessors = settings.GetAllHotkeyAccessors();

            Assert.AreEqual(2, accessors.Length);
            Assert.AreEqual("SoundManager_ActivationShortcut", accessors[0].LocalizationHeaderKey);
            Assert.AreEqual("firefox", accessors[1].LocalizationHeaderKey);
        }

        [TestMethod]
        public void SettingsWrittenBeforeTheShortcutsExistedStillHaveNone()
        {
            var shortcut = new JsonObject
            {
                ["win"] = true,
                ["ctrl"] = true,
                ["alt"] = false,
                ["shift"] = true,
                ["code"] = 86,
            };

            var properties = new JsonObject
            {
                ["activation_shortcut"] = shortcut,
            };

            var json = new JsonObject
            {
                ["name"] = "Sound Manager",
                ["version"] = "1.0",
                ["properties"] = properties,
            };

            var read = Read(json.ToJsonString());

            Assert.AreEqual(0, read.Properties.MuteShortcuts.Value.Count);
            Assert.AreEqual(86, read.Properties.ActivationShortcut.Code);
        }

        private static SoundManagerSettings Read(string json)
        {
            var settings = JsonSerializer.Deserialize<SoundManagerSettings>(json, SettingsSerializationContext.Default.SoundManagerSettings);

            Assert.IsNotNull(settings);

            return settings;
        }
    }
}
