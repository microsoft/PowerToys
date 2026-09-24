// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Models;

namespace ViewModelTests
{
    [TestClass]
    public class ProfileEditorVcpValueRestrictionTests
    {
        [TestMethod]
        public void ColorTemperature_BlockedCurrentValueRequiresAnAllowedSelection()
        {
            var monitor = CreateMonitor();
            monitor.DisabledVcpValues = CreateColorRestrictions(0x05);
            using var viewModel = CreateViewModel(monitor);
            var item = viewModel.Monitors.Single();
            item.IsSelected = true;
            item.IncludeColorTemperature = true;

            Assert.AreEqual(0x08, item.ColorPresetsForDisplay.Single().VcpValue);
            Assert.IsNull(item.ColorTemperature);
            Assert.IsFalse(item.HasValidColorTemperature);
            Assert.IsFalse(viewModel.CanSave);
            Assert.IsNull(viewModel.CreateProfile().MonitorSettings.Single().ColorTemperatureVcp);

            item.ColorTemperature = 0x08;

            Assert.IsTrue(item.HasValidColorTemperature);
            Assert.IsTrue(viewModel.CanSave);
            Assert.AreEqual((int?)0x08, viewModel.CreateProfile().MonitorSettings.Single().ColorTemperatureVcp);
        }

        [TestMethod]
        public void CanSave_InvalidIncludedColorTemperatureBlocksOtherSettingsUntilUnchecked()
        {
            var monitor = CreateMonitor();
            monitor.DisabledVcpValues = CreateColorRestrictions(0x05);
            using var viewModel = CreateViewModel(monitor);
            var item = viewModel.Monitors.Single();
            item.IsSelected = true;
            item.IncludeBrightness = true;
            item.IncludeColorTemperature = true;

            Assert.IsFalse(viewModel.CanSave);
            var settings = viewModel.CreateProfile().MonitorSettings.Single();
            Assert.AreEqual((int?)monitor.CurrentBrightness, settings.Brightness);
            Assert.IsNull(settings.ColorTemperatureVcp);

            item.IncludeColorTemperature = false;

            Assert.IsTrue(viewModel.CanSave);
            Assert.IsNull(viewModel.CreateProfile().MonitorSettings.Single().ColorTemperatureVcp);
        }

        [TestMethod]
        public void ColorTemperature_AllPresetsBlockedCannotBeIncluded()
        {
            var monitor = CreateMonitor();
            monitor.DisabledVcpValues = CreateColorRestrictions(0x05, 0x08);
            using var viewModel = CreateViewModel(monitor);
            var item = viewModel.Monitors.Single();
            item.IsSelected = true;
            item.IncludeColorTemperature = true;

            Assert.AreEqual(0, item.ColorPresetsForDisplay.Count);
            Assert.IsNull(item.ColorTemperature);
            Assert.IsFalse(item.HasValidColorTemperature);
            Assert.IsFalse(viewModel.CanSave);
            Assert.IsNull(viewModel.CreateProfile().MonitorSettings.Single().ColorTemperatureVcp);
        }

        [TestMethod]
        public void ColorTemperature_PrefillingBlockedValuePreservesOriginalSetting()
        {
            const int savedValue = 0x05;
            var monitor = CreateMonitor();
            monitor.ColorTemperatureVcp = 0x08;
            monitor.DisabledVcpValues = CreateColorRestrictions(0x05);
            using var viewModel = CreateViewModel(monitor);
            var item = viewModel.Monitors.Single();
            Assert.AreEqual((int?)0x08, item.ColorTemperature);

            viewModel.PreFillProfile(new PowerDisplayProfile(
                "Existing profile",
                new List<ProfileMonitorSetting> { new(monitor.Id, colorTemperatureVcp: savedValue) }));

            Assert.IsNull(item.ColorTemperature);
            Assert.IsTrue(item.IncludeColorTemperature);
            Assert.IsTrue(item.HasPreservedSettings);
            Assert.IsTrue(viewModel.CanSave);
            Assert.AreEqual((int?)savedValue, viewModel.CreateProfile().MonitorSettings.Single().ColorTemperatureVcp);
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void ColorPresets_UnchangedRestrictionsPreserveSelectionAndInclusion(bool includeColorTemperature)
        {
            var monitor = CreateMonitor();
            using var viewModel = CreateViewModel(monitor);
            var item = viewModel.Monitors.Single();
            item.SuppressAutoSelection = true;
            item.ColorTemperature = 0x08;
            item.SuppressAutoSelection = false;
            item.IncludeColorTemperature = includeColorTemperature;
            var presetChanges = 0;
            item.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(MonitorSelectionItem.ColorPresetsForDisplay))
                {
                    presetChanges++;

                    // Replacing ComboBox.ItemsSource can synchronously clear SelectedValue.
                    item.ColorTemperature = null;
                }
            };

            monitor.DisabledVcpValues = new List<VcpValueBlock>();

            Assert.IsTrue(presetChanges > 0);
            Assert.AreEqual((int?)0x08, item.ColorTemperature);
            Assert.IsTrue(item.HasValidColorTemperature);
            Assert.AreEqual(includeColorTemperature, item.IncludeColorTemperature);
        }

        [TestMethod]
        public void ColorPresets_BlockingTheSelectedValueClearsSelectionAndNotifiesValidation()
        {
            var monitor = CreateMonitor();
            using var viewModel = CreateViewModel(monitor);
            var item = viewModel.Monitors.Single();
            item.IsSelected = true;
            item.ColorTemperature = 0x08;
            Assert.IsTrue(viewModel.CanSave);
            var canSaveChanged = false;
            viewModel.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ProfileEditorViewModel.CanSave))
                {
                    canSaveChanged = true;
                }
            };

            monitor.DisabledVcpValues = CreateColorRestrictions(0x08);

            Assert.IsNull(item.ColorTemperature);
            Assert.IsTrue(item.IncludeColorTemperature);
            Assert.IsFalse(item.HasValidColorTemperature);
            Assert.IsFalse(viewModel.CanSave);
            Assert.IsTrue(canSaveChanged);
            Assert.IsNull(viewModel.CreateProfile().MonitorSettings.Single().ColorTemperatureVcp);

            viewModel.Dispose();
            canSaveChanged = false;
            var itemChanges = 0;
            item.PropertyChanged += (_, _) => itemChanges++;
            monitor.DisabledVcpValues = new List<VcpValueBlock>();

            Assert.IsFalse(canSaveChanged);
            Assert.AreEqual(0, itemChanges);
        }

        [TestMethod]
        public void CanSave_BlockedOriginalColorTemperatureSurvivesSupportChanges()
        {
            var monitor = CreateMonitor();
            monitor.DisabledVcpValues = CreateColorRestrictions(0x05, 0x08);
            using var viewModel = CreateViewModel(monitor);
            viewModel.PreFillProfile(new PowerDisplayProfile(
                "Existing profile",
                new List<ProfileMonitorSetting> { new(monitor.Id, brightness: 25, colorTemperatureVcp: 0x08) }));
            Assert.IsTrue(viewModel.CanSave);
            var canSaveChanges = 0;
            viewModel.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ProfileEditorViewModel.CanSave))
                {
                    canSaveChanges++;
                }
            };

            monitor.SupportsColorTemperature = false;

            Assert.IsTrue(viewModel.CanSave);
            Assert.IsTrue(canSaveChanges > 0);
            canSaveChanges = 0;

            monitor.SupportsColorTemperature = true;

            Assert.IsTrue(viewModel.CanSave);
            Assert.IsTrue(canSaveChanges > 0);
            Assert.AreEqual((int?)0x08, viewModel.CreateProfile().MonitorSettings.Single().ColorTemperatureVcp);
        }

        [TestMethod]
        public void CreateProfile_BlockedOriginalColorTemperatureIsPreservedUntilExplicitlyRemoved()
        {
            var monitor = CreateMonitor();
            using var viewModel = CreateViewModel(monitor);
            viewModel.PreFillProfile(new PowerDisplayProfile(
                "Existing profile",
                new List<ProfileMonitorSetting> { new(monitor.Id, brightness: 25, colorTemperatureVcp: 0x05) }));
            var item = viewModel.Monitors.Single();

            monitor.DisabledVcpValues = CreateColorRestrictions(0x05);

            Assert.IsNull(item.ColorTemperature);
            Assert.IsTrue(item.IncludeColorTemperature);
            Assert.IsTrue(item.HasPreservedSettings);
            Assert.IsTrue(viewModel.CanSave);
            var savedSettings = viewModel.CreateProfile().MonitorSettings.Single();
            Assert.AreEqual((int?)25, savedSettings.Brightness);
            Assert.AreEqual((int?)0x05, savedSettings.ColorTemperatureVcp);

            item.IncludeColorTemperature = false;

            Assert.IsTrue(viewModel.CanSave);
            Assert.IsFalse(item.HasPreservedSettings);
            Assert.IsNull(viewModel.CreateProfile().MonitorSettings.Single().ColorTemperatureVcp);
        }

        [TestMethod]
        public void CanSave_BlockingNewColorTemperatureDoesNotRestoreTheOriginalValue()
        {
            var monitor = CreateMonitor();
            using var viewModel = CreateViewModel(monitor);
            viewModel.PreFillProfile(new PowerDisplayProfile(
                "Existing profile",
                new List<ProfileMonitorSetting> { new(monitor.Id, brightness: 25, colorTemperatureVcp: 0x05) }));
            var item = viewModel.Monitors.Single();
            item.ColorTemperature = 0x08;
            Assert.IsTrue(viewModel.CanSave);
            Assert.AreEqual((int?)0x08, viewModel.CreateProfile().MonitorSettings.Single().ColorTemperatureVcp);

            monitor.DisabledVcpValues = CreateColorRestrictions(0x08);

            Assert.IsNull(item.ColorTemperature);
            Assert.IsTrue(item.IncludeColorTemperature);
            Assert.IsFalse(viewModel.CanSave);
            Assert.IsNull(viewModel.CreateProfile().MonitorSettings.Single().ColorTemperatureVcp);

            item.IncludeColorTemperature = false;

            Assert.IsTrue(viewModel.CanSave);
            Assert.IsNull(viewModel.CreateProfile().MonitorSettings.Single().ColorTemperatureVcp);
        }

        [TestMethod]
        public void ColorTemperature_UnblockingOriginalValueRestoresSelectionAndClearsPreservationNotice()
        {
            var monitor = CreateMonitor();
            using var viewModel = CreateViewModel(monitor);
            viewModel.PreFillProfile(new PowerDisplayProfile(
                "Existing profile",
                new List<ProfileMonitorSetting> { new(monitor.Id, brightness: 25, colorTemperatureVcp: 0x05) }));
            var item = viewModel.Monitors.Single();
            Assert.IsFalse(viewModel.HasPreservedSettings);
            var preservationChanges = 0;
            viewModel.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ProfileEditorViewModel.HasPreservedSettings))
                {
                    preservationChanges++;
                }
            };
            item.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(MonitorSelectionItem.ColorPresetsForDisplay))
                {
                    // Match the synchronous selection clear when ComboBox.ItemsSource changes.
                    item.ColorTemperature = null;
                }
            };

            monitor.DisabledVcpValues = CreateColorRestrictions(0x05);

            Assert.IsNull(item.ColorTemperature);
            Assert.IsTrue(item.IncludeColorTemperature);
            Assert.IsTrue(item.HasPreservedSettings);
            Assert.IsTrue(viewModel.HasPreservedSettings);
            Assert.IsTrue(preservationChanges > 0);
            Assert.AreEqual((int?)0x05, viewModel.CreateProfile().MonitorSettings.Single().ColorTemperatureVcp);
            preservationChanges = 0;

            monitor.DisabledVcpValues = new List<VcpValueBlock>();

            Assert.AreEqual((int?)0x05, item.ColorTemperature);
            Assert.IsTrue(item.IncludeColorTemperature);
            Assert.IsTrue(item.HasValidColorTemperature);
            Assert.IsFalse(item.HasPreservedSettings);
            Assert.IsFalse(viewModel.HasPreservedSettings);
            Assert.IsTrue(preservationChanges > 0);
            Assert.IsTrue(viewModel.CanSave);
            Assert.AreEqual((int?)0x05, viewModel.CreateProfile().MonitorSettings.Single().ColorTemperatureVcp);
        }

        [TestMethod]
        public void CanSave_ChoosingAnotherUnavailableValueDoesNotPreserveBlockedOriginalColorTemperature()
        {
            var monitor = CreateMonitor();
            monitor.DisabledVcpValues = CreateColorRestrictions(0x05, 0x08);
            using var viewModel = CreateViewModel(monitor);
            viewModel.PreFillProfile(new PowerDisplayProfile(
                "Existing profile",
                new List<ProfileMonitorSetting> { new(monitor.Id, brightness: 25, colorTemperatureVcp: 0x05) }));
            var item = viewModel.Monitors.Single();
            Assert.IsNull(item.ColorTemperature);
            Assert.IsTrue(viewModel.CanSave);
            Assert.IsTrue(viewModel.HasPreservedSettings);
            var validationChanges = 0;
            viewModel.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ProfileEditorViewModel.CanSave))
                {
                    validationChanges++;
                }
            };

            item.ColorTemperature = 0x0B;

            Assert.IsNull(item.ColorTemperature);
            Assert.IsTrue(item.IncludeColorTemperature);
            Assert.IsFalse(viewModel.CanSave);
            Assert.IsFalse(viewModel.HasPreservedSettings);
            Assert.IsTrue(validationChanges > 0);
            Assert.IsNull(viewModel.CreateProfile().MonitorSettings.Single().ColorTemperatureVcp);
        }

        private static ProfileEditorViewModel CreateViewModel(MonitorInfo monitor)
        {
            return new ProfileEditorViewModel(new ObservableCollection<MonitorInfo> { monitor }, "Profile");
        }

        private static MonitorInfo CreateMonitor()
        {
            return new MonitorInfo
            {
                Id = "DISPLAY#TEST001#1",
                Name = "Monitor",
                CurrentBrightness = 75,
                ColorTemperatureVcp = 0x05,
                SupportsColorTemperature = true,
                VcpCodesFormatted = CreateColorCapabilities(),
            };
        }

        private static List<VcpCodeDisplayInfo> CreateColorCapabilities(string secondPresetName = "9300 K")
        {
            return new()
            {
                new()
                {
                    Code = "0x14",
                    Title = "Color preset (0x14)",
                    HasValues = true,
                    ValueList = new()
                    {
                        new() { Value = "0x05", Name = "6500 K" },
                        new() { Value = "0x08", Name = secondPresetName },
                    },
                },
            };
        }

        private static List<VcpValueBlock> CreateColorRestrictions(params int[] values)
        {
            return new() { new() { VcpCode = 0x14, Values = values.ToList() } };
        }
    }
}
