// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
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
    public class ProfileEditorViewModelTests
    {
        [TestMethod]
        public void CreateProfile_DefaultProfileId_ReturnsZero()
        {
            using var viewModel = new ProfileEditorViewModel(
                new ObservableCollection<MonitorInfo>(),
                "New profile");

            var profile = viewModel.CreateProfile();

            Assert.AreEqual(0, profile.Id);
        }

        [TestMethod]
        public void CreateProfile_ExistingProfileId_PreservesId()
        {
            const int profileId = 42;
            using var viewModel = new ProfileEditorViewModel(
                new ObservableCollection<MonitorInfo>(),
                "Existing profile",
                profileId);

            var profile = viewModel.CreateProfile();

            Assert.AreEqual(profileId, profile.Id);
        }

        [TestMethod]
        [DataRow(0x0B)]
        public void ColorTemperature_PrefillingAnUnavailableValuePreservesTheOriginalSetting(int savedValue)
        {
            var monitor = CreateMonitor();
            monitor.ColorTemperatureVcp = 0x08;
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
        public void ColorTemperature_ClearingSelectionDoesNotIncludeTheSetting()
        {
            using var viewModel = CreateViewModel(CreateMonitor());
            var item = viewModel.Monitors.Single();
            Assert.IsFalse(item.IncludeColorTemperature);

            item.ColorTemperature = null;

            Assert.IsNull(item.ColorTemperature);
            Assert.IsFalse(item.HasValidColorTemperature);
            Assert.IsFalse(item.IncludeColorTemperature);

            item.ColorTemperature = 0x08;

            Assert.IsTrue(item.HasValidColorTemperature);
            Assert.IsTrue(item.IncludeColorTemperature);
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void ColorPresets_RenamingPreservesSelectionAndInclusion(bool includeColorTemperature)
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

            monitor.VcpCodesFormatted = CreateColorCapabilities("Renamed preset");

            Assert.IsTrue(presetChanges > 0);
            Assert.AreEqual((int?)0x08, item.ColorTemperature);
            Assert.IsTrue(item.HasValidColorTemperature);
            Assert.AreEqual(includeColorTemperature, item.IncludeColorTemperature);
            Assert.AreEqual("Renamed preset", item.ColorPresetsForDisplay.Single(preset => preset.VcpValue == 0x08).DisplayName);
        }

        [TestMethod]
        public void CanSave_UnsupportedColorTemperatureDoesNotBlockBrightness()
        {
            var monitor = CreateMonitor();
            monitor.SupportsColorTemperature = false;
            using var viewModel = CreateViewModel(monitor);
            var item = viewModel.Monitors.Single();
            item.IsSelected = true;
            item.IncludeBrightness = true;
            item.IncludeColorTemperature = true;

            Assert.IsFalse(item.HasValidColorTemperature);
            Assert.IsTrue(viewModel.CanSave);
            var settings = viewModel.CreateProfile().MonitorSettings.Single();
            Assert.AreEqual((int?)monitor.CurrentBrightness, settings.Brightness);
            Assert.IsNull(settings.ColorTemperatureVcp);
        }

        [TestMethod]
        public void PreFillProfile_CaseInsensitiveMonitorIdRestoresEverySettingOnMatchingInstance()
        {
            var monitor = CreateMonitor();
            monitor.Id = @"\\?\DISPLAY#TEST001#FIRST";
            monitor.SupportsContrast = true;
            monitor.SupportsVolume = true;
            var otherMonitor = CreateMonitor();
            otherMonitor.Id = @"\\?\DISPLAY#TEST001#SECOND";
            otherMonitor.CurrentBrightness = 90;
            using var viewModel = new ProfileEditorViewModel(
                new ObservableCollection<MonitorInfo> { otherMonitor, monitor },
                "New profile");

            viewModel.PreFillProfile(new PowerDisplayProfile(
                "Existing profile",
                new List<ProfileMonitorSetting>
                {
                    new(monitor.Id.ToLowerInvariant(), brightness: 25, colorTemperatureVcp: 0x08, contrast: 63, volume: 41),
                }));

            var item = viewModel.Monitors[1];
            Assert.AreSame(monitor, item.Monitor);
            Assert.AreEqual("Existing profile", viewModel.ProfileName);
            Assert.IsTrue(item.IsSelected);
            Assert.IsTrue(item.IncludeBrightness);
            Assert.IsTrue(item.IncludeContrast);
            Assert.IsTrue(item.IncludeVolume);
            Assert.IsTrue(item.IncludeColorTemperature);
            Assert.AreEqual(25, item.Brightness);
            Assert.AreEqual(63, item.Contrast);
            Assert.AreEqual(41, item.Volume);
            Assert.AreEqual((int?)0x08, item.ColorTemperature);
            Assert.IsTrue(viewModel.CanSave);

            var otherItem = viewModel.Monitors[0];
            Assert.AreSame(otherMonitor, otherItem.Monitor);
            Assert.IsFalse(otherItem.IsSelected);
            Assert.IsFalse(otherItem.IncludeBrightness);
            Assert.IsFalse(otherItem.IncludeContrast);
            Assert.IsFalse(otherItem.IncludeVolume);
            Assert.IsFalse(otherItem.IncludeColorTemperature);
            Assert.AreEqual(90, otherItem.Brightness);
            Assert.AreEqual((int?)0x05, otherItem.ColorTemperature);

            var savedSettings = viewModel.CreateProfile().MonitorSettings.Single();
            Assert.IsTrue(MonitorIdComparer.Equal(monitor.Id, savedSettings.MonitorId));
            Assert.AreEqual((int?)25, savedSettings.Brightness);
            Assert.AreEqual((int?)63, savedSettings.Contrast);
            Assert.AreEqual((int?)41, savedSettings.Volume);
            Assert.AreEqual((int?)0x08, savedSettings.ColorTemperatureVcp);
        }

        [TestMethod]
        [DataRow(nameof(ProfileMonitorSetting.Contrast))]
        [DataRow(nameof(ProfileMonitorSetting.Volume))]
        [DataRow(nameof(ProfileMonitorSetting.ColorTemperatureVcp))]
        public void CanSave_PrefillingOnlyAnUnsupportedSettingPreservesIt(string settingName)
        {
            var monitor = CreateMonitor();
            monitor.SupportsContrast = false;
            monitor.SupportsVolume = false;
            monitor.SupportsColorTemperature = false;
            using var viewModel = CreateViewModel(monitor);

            viewModel.PreFillProfile(CreateProfileWithOptionalSetting(monitor.Id, settingName));

            var item = viewModel.Monitors.Single();
            Assert.IsTrue(item.IsSelected);
            Assert.AreEqual(settingName == nameof(ProfileMonitorSetting.Contrast), item.IncludeContrast);
            Assert.AreEqual(settingName == nameof(ProfileMonitorSetting.Volume), item.IncludeVolume);
            Assert.AreEqual(settingName == nameof(ProfileMonitorSetting.ColorTemperatureVcp), item.IncludeColorTemperature);
            Assert.IsTrue(item.HasPreservedSettings);
            Assert.IsTrue(viewModel.HasValidSettings);
            Assert.IsTrue(viewModel.CanSave);
            var savedSettings = viewModel.CreateProfile().MonitorSettings.Single();
            Assert.IsNull(savedSettings.Brightness);
            Assert.AreEqual(settingName == nameof(ProfileMonitorSetting.Contrast) ? (int?)63 : null, savedSettings.Contrast);
            Assert.AreEqual(settingName == nameof(ProfileMonitorSetting.Volume) ? (int?)41 : null, savedSettings.Volume);
            Assert.AreEqual(settingName == nameof(ProfileMonitorSetting.ColorTemperatureVcp) ? (int?)0x08 : null, savedSettings.ColorTemperatureVcp);
            Assert.AreEqual(item.IncludeContrast, item.ShowContrast);
            Assert.AreEqual(item.IncludeVolume, item.ShowVolume);
            Assert.AreEqual(item.IncludeColorTemperature, item.ShowColorTemperature);
        }

        [TestMethod]
        public void CanSave_EverySelectedMonitorNeedsAPersistedSetting()
        {
            var monitor = CreateMonitor();
            var unsupportedMonitor = CreateMonitor();
            unsupportedMonitor.Id = "DISPLAY#TEST001#2";
            unsupportedMonitor.SupportsContrast = false;
            using var viewModel = new ProfileEditorViewModel(
                new ObservableCollection<MonitorInfo> { monitor, unsupportedMonitor },
                "Profile");
            viewModel.Monitors[0].IsSelected = true;
            viewModel.Monitors[0].Brightness = 25;
            viewModel.Monitors[1].IsSelected = true;
            viewModel.Monitors[1].Contrast = 63;

            Assert.IsTrue(viewModel.Monitors.All(item => item.IsSelected));
            Assert.IsFalse(viewModel.CanSave);

            viewModel.Monitors[1].IsSelected = false;

            Assert.IsTrue(viewModel.CanSave);
            var savedSettings = viewModel.CreateProfile().MonitorSettings.Single();
            Assert.AreEqual(monitor.Id, savedSettings.MonitorId);
            Assert.AreEqual((int?)25, savedSettings.Brightness);
        }

        [TestMethod]
        [DataRow(nameof(ProfileMonitorSetting.Contrast))]
        [DataRow(nameof(ProfileMonitorSetting.Volume))]
        public void CanSave_OriginalOptionalSettingSurvivesSupportChanges(string settingName)
        {
            var monitor = CreateMonitor();
            monitor.SupportsContrast = true;
            monitor.SupportsVolume = true;
            using var viewModel = CreateViewModel(monitor);
            viewModel.PreFillProfile(CreateProfileWithOptionalSetting(monitor.Id, settingName));
            Assert.IsTrue(viewModel.CanSave);
            var canSaveChanges = 0;
            viewModel.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ProfileEditorViewModel.CanSave))
                {
                    canSaveChanges++;
                }
            };

            if (settingName == nameof(ProfileMonitorSetting.Contrast))
            {
                monitor.SupportsContrast = false;
            }
            else
            {
                monitor.SupportsVolume = false;
            }

            Assert.IsTrue(viewModel.CanSave);
            Assert.IsTrue(viewModel.Monitors.Single().HasPreservedSettings);
            var savedSettings = viewModel.CreateProfile().MonitorSettings.Single();
            Assert.AreEqual(settingName == nameof(ProfileMonitorSetting.Contrast) ? (int?)63 : null, savedSettings.Contrast);
            Assert.AreEqual(settingName == nameof(ProfileMonitorSetting.Volume) ? (int?)41 : null, savedSettings.Volume);
            Assert.IsTrue(canSaveChanges > 0);
            canSaveChanges = 0;

            if (settingName == nameof(ProfileMonitorSetting.Contrast))
            {
                monitor.SupportsContrast = true;
            }
            else
            {
                monitor.SupportsVolume = true;
            }

            Assert.IsTrue(viewModel.CanSave);
            Assert.IsFalse(viewModel.Monitors.Single().HasPreservedSettings);
            Assert.IsTrue(canSaveChanges > 0);
        }

        [TestMethod]
        public void CreateProfile_RenamingPreservesMetadataAndAnIndependentCopyOfOriginalSettings()
        {
            var monitor = CreateMonitor();
            monitor.SupportsBrightness = false;
            monitor.SupportsContrast = false;
            monitor.SupportsVolume = false;
            monitor.SupportsColorTemperature = false;
            var createdDate = new DateTime(2024, 3, 15, 10, 30, 0, DateTimeKind.Utc);
            var originalSetting = new ProfileMonitorSetting(monitor.Id, brightness: 25, colorTemperatureVcp: 0x08, contrast: 63, volume: 41);
            var original = new PowerDisplayProfile("Original profile", new List<ProfileMonitorSetting> { originalSetting })
            {
                Id = 42,
                CreatedDate = createdDate,
            };
            using var viewModel = new ProfileEditorViewModel(
                new ObservableCollection<MonitorInfo> { monitor },
                "Existing profile",
                original.Id);
            viewModel.PreFillProfile(original);
            viewModel.ProfileName = "Renamed profile";

            Assert.AreEqual("Original profile", original.Name);
            Assert.IsTrue(viewModel.CanSave);
            var item = viewModel.Monitors.Single();
            Assert.IsTrue(item.HasPreservedSettings);
            Assert.IsTrue(item.ShowBrightness);
            Assert.IsTrue(item.ShowContrast);
            Assert.IsTrue(item.ShowVolume);
            Assert.IsTrue(item.ShowColorTemperature);

            originalSetting.Brightness = 99;
            originalSetting.ColorTemperatureVcp = 0x0B;
            originalSetting.Contrast = null;
            originalSetting.Volume = null;
            original.Id = 7;
            original.CreatedDate = DateTime.MinValue;
            original.MonitorSettings.Clear();

            var saved = viewModel.CreateProfile();
            Assert.AreEqual(42, saved.Id);
            Assert.AreEqual(createdDate, saved.CreatedDate);
            Assert.AreEqual("Renamed profile", saved.Name);
            var savedSettings = saved.MonitorSettings.Single();
            Assert.AreNotSame(originalSetting, savedSettings);
            Assert.AreEqual(monitor.Id, savedSettings.MonitorId);
            Assert.AreEqual((int?)25, savedSettings.Brightness);
            Assert.AreEqual((int?)0x08, savedSettings.ColorTemperatureVcp);
            Assert.AreEqual((int?)63, savedSettings.Contrast);
            Assert.AreEqual((int?)41, savedSettings.Volume);

            savedSettings.Brightness = 1;
            savedSettings.ColorTemperatureVcp = 0x0B;
            savedSettings.Contrast = 2;
            savedSettings.Volume = 3;
            var savedAgain = viewModel.CreateProfile().MonitorSettings.Single();
            Assert.AreNotSame(savedSettings, savedAgain);
            Assert.AreEqual((int?)25, savedAgain.Brightness);
            Assert.AreEqual((int?)0x08, savedAgain.ColorTemperatureVcp);
            Assert.AreEqual((int?)63, savedAgain.Contrast);
            Assert.AreEqual((int?)41, savedAgain.Volume);
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void CreateProfile_RenamingPreservesMissingMonitors(bool includeConnectedMonitor)
        {
            var monitor = CreateMonitor();
            const string missingMonitorId = "DISPLAY#TEST001#MISSING";
            var availableMonitors = new ObservableCollection<MonitorInfo>();
            if (includeConnectedMonitor)
            {
                availableMonitors.Add(monitor);
            }

            var original = new PowerDisplayProfile(
                "Original profile",
                new List<ProfileMonitorSetting>
                {
                    new(monitor.Id, brightness: 25),
                    new(missingMonitorId, colorTemperatureVcp: 0x0B, contrast: 63, volume: 41),
                });
            using var viewModel = new ProfileEditorViewModel(availableMonitors, "Existing profile");
            viewModel.PreFillProfile(original);
            viewModel.ProfileName = "Renamed profile";
            original.MonitorSettings[0].Brightness = 90;
            original.MonitorSettings[1].Volume = 10;

            Assert.IsTrue(viewModel.CanSave);
            var saved = viewModel.CreateProfile();
            Assert.AreEqual("Renamed profile", saved.Name);
            Assert.AreEqual(2, saved.MonitorSettings.Count);
            var connectedSettings = saved.MonitorSettings.Single(setting => setting.MonitorId == monitor.Id);
            Assert.AreEqual((int?)25, connectedSettings.Brightness);
            Assert.IsNull(connectedSettings.ColorTemperatureVcp);
            Assert.IsNull(connectedSettings.Contrast);
            Assert.IsNull(connectedSettings.Volume);
            var missingSettings = saved.MonitorSettings.Single(setting => setting.MonitorId == missingMonitorId);
            Assert.IsNull(missingSettings.Brightness);
            Assert.AreEqual((int?)0x0B, missingSettings.ColorTemperatureVcp);
            Assert.AreEqual((int?)63, missingSettings.Contrast);
            Assert.AreEqual((int?)41, missingSettings.Volume);
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void CreateProfile_DeselectingAnOriginalMonitorExplicitlyRemovesIt(bool includeMissingMonitor)
        {
            var monitor = CreateMonitor();
            const string missingMonitorId = "DISPLAY#TEST001#MISSING";
            var originalSettings = new List<ProfileMonitorSetting> { new(monitor.Id, brightness: 25) };
            if (includeMissingMonitor)
            {
                originalSettings.Add(new ProfileMonitorSetting(missingMonitorId, brightness: 50));
            }

            using var viewModel = CreateViewModel(monitor);
            viewModel.PreFillProfile(new PowerDisplayProfile("Existing profile", originalSettings));
            viewModel.Monitors.Single().IsSelected = false;

            Assert.AreEqual(includeMissingMonitor, viewModel.CanSave);
            var saved = viewModel.CreateProfile();
            Assert.AreEqual(includeMissingMonitor ? 1 : 0, saved.MonitorSettings.Count);
            Assert.IsFalse(saved.MonitorSettings.Any(setting => MonitorIdComparer.Equal(setting.MonitorId, monitor.Id)));
            if (includeMissingMonitor)
            {
                Assert.AreEqual(missingMonitorId, saved.MonitorSettings.Single().MonitorId);
                Assert.AreEqual((int?)50, saved.MonitorSettings.Single().Brightness);
            }

            Assert.IsFalse(viewModel.CreateProfile().MonitorSettings.Any(setting => MonitorIdComparer.Equal(setting.MonitorId, monitor.Id)));
        }

        [TestMethod]
        [DataRow(nameof(ProfileMonitorSetting.Contrast))]
        [DataRow(nameof(ProfileMonitorSetting.Volume))]
        [DataRow(nameof(ProfileMonitorSetting.ColorTemperatureVcp))]
        public void CanSave_NewUnsupportedOnlySettingDoesNotAllowEmptyMonitorSettings(string settingName)
        {
            var monitor = CreateMonitor();
            monitor.SupportsContrast = false;
            monitor.SupportsVolume = false;
            monitor.SupportsColorTemperature = false;
            using var viewModel = CreateViewModel(monitor);
            var item = viewModel.Monitors.Single();
            item.IsSelected = true;
            item.IncludeContrast = settingName == nameof(ProfileMonitorSetting.Contrast);
            item.IncludeVolume = settingName == nameof(ProfileMonitorSetting.Volume);
            item.IncludeColorTemperature = settingName == nameof(ProfileMonitorSetting.ColorTemperatureVcp);

            Assert.IsFalse(item.HasPreservedSettings);
            Assert.IsFalse(viewModel.HasValidSettings);
            Assert.IsFalse(viewModel.CanSave);
            var savedSettings = viewModel.CreateProfile().MonitorSettings.Single();
            Assert.IsNull(savedSettings.Brightness);
            Assert.IsNull(savedSettings.Contrast);
            Assert.IsNull(savedSettings.Volume);
            Assert.IsNull(savedSettings.ColorTemperatureVcp);
        }

        [TestMethod]
        [DataRow(nameof(ProfileMonitorSetting.Contrast))]
        [DataRow(nameof(ProfileMonitorSetting.Volume))]
        public void CreateProfile_OriginalUnsupportedOptionalSettingCanBeEditedOrExplicitlyRemoved(string settingName)
        {
            var monitor = CreateMonitor();
            monitor.SupportsContrast = true;
            monitor.SupportsVolume = true;
            var original = CreateProfileWithOptionalSetting(monitor.Id, settingName);
            original.MonitorSettings.Single().Brightness = 25;
            using var viewModel = CreateViewModel(monitor);
            viewModel.PreFillProfile(original);
            var item = viewModel.Monitors.Single();

            if (settingName == nameof(ProfileMonitorSetting.Contrast))
            {
                monitor.SupportsContrast = false;
                item.Contrast = 74;
            }
            else
            {
                monitor.SupportsVolume = false;
                item.Volume = 74;
            }

            Assert.IsTrue(viewModel.CanSave);
            var editedSettings = viewModel.CreateProfile().MonitorSettings.Single();
            Assert.AreEqual((int?)25, editedSettings.Brightness);
            Assert.AreEqual(settingName == nameof(ProfileMonitorSetting.Contrast) ? (int?)74 : null, editedSettings.Contrast);
            Assert.AreEqual(settingName == nameof(ProfileMonitorSetting.Volume) ? (int?)74 : null, editedSettings.Volume);

            item.IncludeContrast = false;
            item.IncludeVolume = false;

            Assert.IsTrue(viewModel.CanSave);
            Assert.IsFalse(item.HasPreservedSettings);
            var removedSettings = viewModel.CreateProfile().MonitorSettings.Single();
            Assert.AreEqual((int?)25, removedSettings.Brightness);
            Assert.IsNull(removedSettings.Contrast);
            Assert.IsNull(removedSettings.Volume);
        }

        [TestMethod]
        [DataRow("Unsupported")]
        [DataRow("MissingCapabilities")]
        public void CreateProfile_OriginalColorTemperatureSurvivesBecomingUnavailableUntilExplicitlyRemoved(string unavailableReason)
        {
            var monitor = CreateMonitor();
            using var viewModel = CreateViewModel(monitor);
            viewModel.PreFillProfile(new PowerDisplayProfile(
                "Existing profile",
                new List<ProfileMonitorSetting> { new(monitor.Id, brightness: 25, colorTemperatureVcp: 0x05) }));
            var item = viewModel.Monitors.Single();

            MakeColorTemperatureUnavailable(monitor, unavailableReason);

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
        [DataRow("Unsupported")]
        [DataRow("MissingCapabilities")]
        public void CanSave_NewColorTemperatureBecomingUnavailableDoesNotRestoreTheOriginalValue(string unavailableReason)
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

            MakeColorTemperatureUnavailable(monitor, unavailableReason);

            Assert.IsNull(item.ColorTemperature);
            Assert.IsTrue(item.IncludeColorTemperature);
            Assert.IsFalse(viewModel.CanSave);
            Assert.IsNull(viewModel.CreateProfile().MonitorSettings.Single().ColorTemperatureVcp);

            item.IncludeColorTemperature = false;

            Assert.IsTrue(viewModel.CanSave);
            Assert.IsNull(viewModel.CreateProfile().MonitorSettings.Single().ColorTemperatureVcp);
        }

        [TestMethod]
        public void CanSave_ExplicitlySelectingAnUnsupportedColorTemperatureBlocksBrightness()
        {
            var monitor = CreateMonitor();
            monitor.SupportsColorTemperature = false;
            using var viewModel = CreateViewModel(monitor);
            var item = viewModel.Monitors.Single();
            item.IsSelected = true;
            item.IncludeBrightness = true;
            item.IncludeColorTemperature = true;

            item.ColorTemperature = 0x08;

            Assert.IsNull(item.ColorTemperature);
            Assert.IsFalse(viewModel.CanSave);
            var savedSettings = viewModel.CreateProfile().MonitorSettings.Single();
            Assert.AreEqual((int?)monitor.CurrentBrightness, savedSettings.Brightness);
            Assert.IsNull(savedSettings.ColorTemperatureVcp);
        }

        [TestMethod]
        [DataRow("Unsupported")]
        [DataRow("MissingCapabilities")]
        public void ColorTemperature_RestoringOriginalAvailabilityRestoresSelectionAndClearsPreservationNotice(string unavailableReason)
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

            MakeColorTemperatureUnavailable(monitor, unavailableReason);

            Assert.IsNull(item.ColorTemperature);
            Assert.IsTrue(item.IncludeColorTemperature);
            Assert.IsTrue(item.HasPreservedSettings);
            Assert.IsTrue(viewModel.HasPreservedSettings);
            Assert.IsTrue(preservationChanges > 0);
            Assert.AreEqual((int?)0x05, viewModel.CreateProfile().MonitorSettings.Single().ColorTemperatureVcp);
            preservationChanges = 0;

            switch (unavailableReason)
            {
                case "Unsupported":
                    monitor.SupportsColorTemperature = true;
                    break;
                case "MissingCapabilities":
                    monitor.VcpCodesFormatted = CreateColorCapabilities();
                    break;
            }

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
        public void CanSave_IncludingTheCurrentColorTemperatureCountsAsANewChoiceWhenSupportDisappears()
        {
            var monitor = CreateMonitor();
            using var viewModel = CreateViewModel(monitor);
            var item = viewModel.Monitors.Single();
            item.IsSelected = true;
            item.IncludeBrightness = true;
            item.IncludeColorTemperature = true;
            Assert.AreEqual((int?)0x05, item.ColorTemperature);
            Assert.IsTrue(viewModel.CanSave);

            monitor.SupportsColorTemperature = false;

            Assert.IsNull(item.ColorTemperature);
            Assert.IsTrue(item.IncludeColorTemperature);
            Assert.IsFalse(viewModel.CanSave);
            var savedSettings = viewModel.CreateProfile().MonitorSettings.Single();
            Assert.AreEqual((int?)monitor.CurrentBrightness, savedSettings.Brightness);
            Assert.IsNull(savedSettings.ColorTemperatureVcp);

            item.IncludeColorTemperature = false;

            Assert.IsTrue(viewModel.CanSave);
        }

        [TestMethod]
        public void Dispose_MonitorChangesDoNotNotifyTheEditor()
        {
            var monitor = CreateMonitor();
            using var viewModel = CreateViewModel(monitor);
            var item = viewModel.Monitors.Single();
            viewModel.Dispose();
            var itemChanges = 0;
            var editorChanges = 0;
            item.PropertyChanged += (_, _) => itemChanges++;
            viewModel.PropertyChanged += (_, _) => editorChanges++;

            monitor.VcpCodesFormatted = CreateColorCapabilities("Renamed preset");
            monitor.SupportsContrast = true;
            monitor.SupportsVolume = true;
            monitor.SupportsColorTemperature = false;

            Assert.AreEqual(0, itemChanges);
            Assert.AreEqual(0, editorChanges);
        }

        private static ProfileEditorViewModel CreateViewModel(MonitorInfo monitor)
        {
            return new ProfileEditorViewModel(new ObservableCollection<MonitorInfo> { monitor }, "Profile");
        }

        private static PowerDisplayProfile CreateProfileWithOptionalSetting(string monitorId, string settingName)
        {
            return new PowerDisplayProfile(
                "Existing profile",
                new List<ProfileMonitorSetting>
                {
                    new(
                        monitorId,
                        colorTemperatureVcp: settingName == nameof(ProfileMonitorSetting.ColorTemperatureVcp) ? 0x08 : null,
                        contrast: settingName == nameof(ProfileMonitorSetting.Contrast) ? 63 : null,
                        volume: settingName == nameof(ProfileMonitorSetting.Volume) ? 41 : null),
                });
        }

        private static void MakeColorTemperatureUnavailable(MonitorInfo monitor, string reason)
        {
            switch (reason)
            {
                case "Unsupported":
                    monitor.SupportsColorTemperature = false;
                    break;
                case "MissingCapabilities":
                    monitor.VcpCodesFormatted = new List<VcpCodeDisplayInfo>();
                    break;
                default:
                    Assert.Fail($"Unknown unavailable reason: {reason}");
                    break;
            }
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
    }
}
