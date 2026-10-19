// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CommonLibTest
{
    [TestClass]
    public class CursorBrakeFullScreenSettingsTests
    {
        [TestMethod]
        public void NewSettingsTreatFullScreenAppsLikeOtherAppsAndExcludeNothing()
        {
            var properties = new CursorBrakeProperties();

            Assert.AreEqual(0, properties.FullscreenBehavior.Value, "The default is Same as other apps.");
            Assert.AreEqual(string.Empty, properties.ExcludedApps.Value, "No app is excluded by default.");
        }

        [TestMethod]
        public void BothSettingsUseTheKeysTheModuleReads()
        {
            var settings = new CursorBrakeSettings();
            settings.Properties.FullscreenBehavior.Value = 1;
            settings.Properties.ExcludedApps.Value = "game.exe\r\noutlook.exe";

            using JsonDocument document = JsonDocument.Parse(settings.ToJsonString());
            JsonElement properties = document.RootElement.GetProperty("properties");

            Assert.AreEqual(1, properties.GetProperty("fullscreen_behavior").GetProperty("value").GetInt32());
            Assert.AreEqual("game.exe\r\noutlook.exe", properties.GetProperty("excluded_apps").GetProperty("value").GetString());
        }

        [TestMethod]
        [DataRow(0, "")]
        [DataRow(1, "game.exe")]
        [DataRow(2, "Game.exe\r\n\r\n  outlook.exe  \n")]
        public void BothSettingsRoundTrip(int behavior, string excludedApps)
        {
            var settings = new CursorBrakeSettings();
            settings.Properties.FullscreenBehavior.Value = behavior;
            settings.Properties.ExcludedApps.Value = excludedApps;

            CursorBrakeSettings read = JsonSerializer.Deserialize(settings.ToJsonString(), SettingsSerializationContext.Default.CursorBrakeSettings);

            Assert.AreEqual(behavior, read.Properties.FullscreenBehavior.Value);
            Assert.AreEqual(excludedApps, read.Properties.ExcludedApps.Value, "The text is kept as typed; the module trims and uppercases it.");
        }

        [TestMethod]
        public void AFileFromBeforeTheSettingsKeepsTheDefaults()
        {
            var read = JsonSerializer.Deserialize<CursorBrakeProperties>("""{ "push_distance": { "value": 450 }, "cross_modifier": { "value": 1 } }""");

            Assert.AreEqual(450, read.PushDistance.Value);
            Assert.AreEqual(0, read.FullscreenBehavior.Value);
            Assert.AreEqual(string.Empty, read.ExcludedApps.Value);
        }

        [TestMethod]
        [DataRow(0, 0)]
        [DataRow(1, 1)]
        [DataRow(2, 2)]
        [DataRow(3, 0)]
        [DataRow(-1, 0)]
        [DataRow(int.MaxValue, 0)]
        [DataRow(int.MinValue, 0)]
        public void TheViewModelRepairsAChoiceTheModuleWouldIgnore(int stored, int expected)
        {
            Assert.AreEqual(expected, MouseUtilsViewModel.RepairCursorBrakeChoice<CursorBrakeFullscreenBehavior>(stored));
        }

        [TestMethod]
        [DataRow(1, 1, 1)]
        [DataRow(2, 2, 0)]
        [DataRow(3, 0, 0)]
        [DataRow(-1, 0, 0)]
        public void TheViewModelRepairsEachChoiceAgainstItsOwnRange(int stored, int edgeFilter, int crossModifier)
        {
            Assert.AreEqual(edgeFilter, MouseUtilsViewModel.RepairCursorBrakeChoice<CursorBrakeEdgeFilter>(stored), "Apply to has three choices.");
            Assert.AreEqual(crossModifier, MouseUtilsViewModel.RepairCursorBrakeChoice<CursorBrakeCrossModifier>(stored), "The cross key has two.");
        }

        // Each choice box binds SelectedIndex to the stored number, so the enums keep the module's
        // numbering and the boxes' item order: inserting a choice anywhere but at the end would
        // remap existing settings.
        [TestMethod]
        public void ChoiceValuesMatchTheModuleAndTheBoxOrder()
        {
            Assert.AreEqual(0, (int)CursorBrakeEdgeFilter.All);
            Assert.AreEqual(1, (int)CursorBrakeEdgeFilter.SideBySide);
            Assert.AreEqual(2, (int)CursorBrakeEdgeFilter.Stacked);
            Assert.AreEqual(0, (int)CursorBrakeCrossModifier.Ctrl);
            Assert.AreEqual(1, (int)CursorBrakeCrossModifier.Shift);
            Assert.AreEqual(0, (int)CursorBrakeFullscreenBehavior.Same);
            Assert.AreEqual(1, (int)CursorBrakeFullscreenBehavior.Wall);
            Assert.AreEqual(2, (int)CursorBrakeFullscreenBehavior.NoResistance);
        }
    }
}
