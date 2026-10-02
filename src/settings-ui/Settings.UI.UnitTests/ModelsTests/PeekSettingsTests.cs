// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;

using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CommonLibTest
{
    [TestClass]
    public class PeekSettingsTests
    {
        [TestMethod]
        public void ExistingSettingsWithoutAudioVolumeKeepTheDefault()
        {
            var settings = JsonSerializer.Deserialize("{\"name\":\"Peek\",\"version\":\"0.0.2\",\"properties\":{}}", SettingsSerializationContext.Default.PeekSettings);

            Assert.AreEqual(1.0, settings.Properties.AudioVolume.Value);
        }

        [TestMethod]
        [DataRow(0.0)]
        [DataRow(0.2)]
        [DataRow(1.0)]
        public void AudioVolumeSurvivesSettingsSerialization(double volume)
        {
            var settings = new PeekSettings();
            settings.Properties.AudioVolume.Value = volume;

            var restored = JsonSerializer.Deserialize(settings.ToJsonString(), SettingsSerializationContext.Default.PeekSettings);

            Assert.AreEqual(volume, restored.Properties.AudioVolume.Value);
            Assert.AreEqual(settings.Properties.ActivationShortcut.Code, restored.Properties.ActivationShortcut.Code);
            Assert.AreEqual(settings.Properties.ConfirmFileDelete.Value, restored.Properties.ConfirmFileDelete.Value);
        }
    }
}
