// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;

using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ViewModelTests
{
    [TestClass]
    public class AlwaysOnTopSettingsTests
    {
        [TestMethod]
        public void JsonRoundTrip_PreservesAllFields()
        {
            var original = new AlwaysOnTopProperties
            {
                FrameThickness = new IntProperty(25),
                FrameColor = new StringProperty("#FF0000"),
                FrameOpacity = new IntProperty(80),
                SoundEnabled = new BoolProperty(false),
                ExcludedApps = new StringProperty("notepad.exe"),
            };

            var json = JsonSerializer.Serialize(original);
            var deserialized = JsonSerializer.Deserialize<AlwaysOnTopProperties>(json);

            Assert.IsNotNull(deserialized);
            Assert.AreEqual(original.FrameEnabled.Value, deserialized.FrameEnabled.Value);
            Assert.AreEqual(original.ShowInSystemMenu.Value, deserialized.ShowInSystemMenu.Value);
            Assert.AreEqual(original.FrameThickness.Value, deserialized.FrameThickness.Value);
            Assert.AreEqual(original.FrameColor.Value, deserialized.FrameColor.Value);
            Assert.AreEqual(original.FrameAccentColor.Value, deserialized.FrameAccentColor.Value);
            Assert.AreEqual(original.FrameOpacity.Value, deserialized.FrameOpacity.Value);
            Assert.AreEqual(original.SoundEnabled.Value, deserialized.SoundEnabled.Value);
            Assert.AreEqual(original.DoNotActivateOnGameMode.Value, deserialized.DoNotActivateOnGameMode.Value);
            Assert.AreEqual(original.RoundCornersEnabled.Value, deserialized.RoundCornersEnabled.Value);
            Assert.AreEqual(original.ExcludedApps.Value, deserialized.ExcludedApps.Value);
            Assert.AreEqual(original.Hotkey.Value.Win, deserialized.Hotkey.Value.Win);
            Assert.AreEqual(original.Hotkey.Value.Code, deserialized.Hotkey.Value.Code);
        }
    }
}
