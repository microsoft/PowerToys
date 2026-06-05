// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;

using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ViewModelTests
{
    [TestClass]
    public class FindMyMouseSettingsTests
    {
        [TestMethod]
        public void JsonRoundTrip_PreservesAllFields()
        {
            var original = new FindMyMouseProperties
            {
                ActivationMethod = new IntProperty(1),
                IncludeWinKey = new BoolProperty(true),
                DoNotActivateOnGameMode = new BoolProperty(false),
                BackgroundColor = new StringProperty("#90000000"),
                SpotlightColor = new StringProperty("#90FFFFFF"),
                SpotlightRadius = new IntProperty(150),
                AnimationDurationMs = new IntProperty(700),
                SpotlightInitialZoom = new IntProperty(7),
                ExcludedApps = new StringProperty("game.exe"),
                ShakingMinimumDistance = new IntProperty(1200),
                ShakingIntervalMs = new IntProperty(800),
                ShakingFactor = new IntProperty(350),
            };

            var json = JsonSerializer.Serialize(original);
            var deserialized = JsonSerializer.Deserialize<FindMyMouseProperties>(json);

            Assert.IsNotNull(deserialized);
            Assert.AreEqual(original.ActivationMethod.Value, deserialized.ActivationMethod.Value);
            Assert.AreEqual(original.IncludeWinKey.Value, deserialized.IncludeWinKey.Value);
            Assert.AreEqual(original.DoNotActivateOnGameMode.Value, deserialized.DoNotActivateOnGameMode.Value);
            Assert.AreEqual(original.BackgroundColor.Value, deserialized.BackgroundColor.Value);
            Assert.AreEqual(original.SpotlightColor.Value, deserialized.SpotlightColor.Value);
            Assert.AreEqual(original.SpotlightRadius.Value, deserialized.SpotlightRadius.Value);
            Assert.AreEqual(original.AnimationDurationMs.Value, deserialized.AnimationDurationMs.Value);
            Assert.AreEqual(original.SpotlightInitialZoom.Value, deserialized.SpotlightInitialZoom.Value);
            Assert.AreEqual(original.ExcludedApps.Value, deserialized.ExcludedApps.Value);
            Assert.AreEqual(original.ShakingMinimumDistance.Value, deserialized.ShakingMinimumDistance.Value);
            Assert.AreEqual(original.ShakingIntervalMs.Value, deserialized.ShakingIntervalMs.Value);
            Assert.AreEqual(original.ShakingFactor.Value, deserialized.ShakingFactor.Value);
            Assert.AreEqual(original.ActivationShortcut.Code, deserialized.ActivationShortcut.Code);
        }
    }
}
