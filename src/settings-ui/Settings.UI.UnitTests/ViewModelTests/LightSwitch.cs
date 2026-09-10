// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.UnitTests.BackwardsCompatibility;
using Microsoft.PowerToys.Settings.UI.UnitTests.Mocks;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Models;
using Settings.UI.Library.Helpers;

namespace ViewModelTests;

[TestClass]
public class LightSwitch
{
    [TestMethod]
    [DataRow("FixedHours")]
    [DataRow("Off")]
    [DataRow("FollowNightLight")]
    public void ModuleSettingsRefreshUpdatesTimesAndClearsSunMarkersWithoutSaving(string nextMode)
    {
        var messages = new List<string>();
        using var viewModel = CreateViewModel(new LightSwitchSettings(), message =>
        {
            messages.Add(message);
            return 0;
        });
        var city = new SearchLocation("Hong Kong", "China", 22.3, 114.2);
        viewModel.SearchLocations.Add(city);
        var notifications = new HashSet<string>();
        viewModel.PropertyChanged += (_, e) => notifications.Add(e.PropertyName);

        var sunSettings = new LightSwitchSettings();
        sunSettings.Properties.ScheduleMode.Value = "SunsetToSunrise";
        sunSettings.Properties.Latitude.Value = "22.3";
        sunSettings.Properties.Longitude.Value = "114.2";
        sunSettings.Properties.LightTime.Value = 367;
        sunSettings.Properties.DarkTime.Value = 1105;
        sunSettings.Properties.SunriseOffset.Value = 15;
        sunSettings.Properties.SunsetOffset.Value = -20;
        string savedSunSettings = sunSettings.ToJsonString();

        viewModel.ModuleSettings = sunSettings;

        Assert.AreEqual(savedSunSettings, sunSettings.ToJsonString());
        Assert.AreEqual(TimeSpan.FromMinutes(367), viewModel.LightTimePickerValue);
        Assert.AreEqual(TimeSpan.FromMinutes(1105), viewModel.DarkTimePickerValue);
        Assert.AreEqual(TimeSpan.FromMinutes(382), viewModel.LightTimeTimeSpan);
        Assert.AreEqual(TimeSpan.FromMinutes(1085), viewModel.DarkTimeTimeSpan);
        Assert.AreEqual<TimeSpan?>(TimeSpan.FromMinutes(367), viewModel.SunriseTimeSpan);
        Assert.AreEqual<TimeSpan?>(TimeSpan.FromMinutes(1105), viewModel.SunsetTimeSpan);
        Assert.AreEqual(-367, viewModel.SunriseOffsetMin);
        Assert.AreEqual(717, viewModel.SunriseOffsetMax);
        Assert.AreEqual(-722, viewModel.SunsetOffsetMin);
        Assert.AreEqual(334, viewModel.SunsetOffsetMax);
        Assert.AreSame(city, viewModel.SelectedCity);
        Assert.AreEqual(city.City, viewModel.SyncButtonInformation);
        AssertTimeBindingsNotified(notifications);
        Assert.IsTrue(notifications.Contains(nameof(viewModel.SyncButtonInformation)));

        notifications.Clear();
        var nextSettings = (LightSwitchSettings)sunSettings.Clone();
        nextSettings.Properties.ScheduleMode.Value = nextMode;
        nextSettings.Properties.LightTime.Value = 503;
        nextSettings.Properties.DarkTime.Value = 1259;
        string savedNextSettings = nextSettings.ToJsonString();

        viewModel.ModuleSettings = nextSettings;

        Assert.AreEqual(savedNextSettings, nextSettings.ToJsonString());
        Assert.AreEqual(TimeSpan.FromMinutes(503), viewModel.LightTimePickerValue);
        Assert.AreEqual(TimeSpan.FromMinutes(1259), viewModel.DarkTimePickerValue);
        Assert.AreEqual(TimeSpan.FromMinutes(503), viewModel.LightTimeTimeSpan);
        Assert.AreEqual(TimeSpan.FromMinutes(1259), viewModel.DarkTimeTimeSpan);
        Assert.IsNull(viewModel.SunriseTimeSpan);
        Assert.IsNull(viewModel.SunsetTimeSpan);
        AssertTimeBindingsNotified(notifications);
        Assert.AreEqual(0, messages.Count);
    }

    [TestMethod]
    public void InitialSunScheduleDisplayCalculatesTodayWithoutChangingSavedTimes()
    {
        var settings = new LightSwitchSettings();
        settings.Properties.ScheduleMode.Value = "SunsetToSunrise";
        settings.Properties.Latitude.Value = "22.3";
        settings.Properties.Longitude.Value = "114.2";
        settings.Properties.LightTime.Value = 503;
        settings.Properties.DarkTime.Value = 1259;
        settings.Properties.SunriseOffset.Value = 15;
        settings.Properties.SunsetOffset.Value = -20;
        string savedSettings = settings.ToJsonString();
        var messages = new List<string>();
        using var viewModel = CreateViewModel(settings, message =>
        {
            messages.Add(message);
            return 0;
        });
        viewModel.SearchLocations.Add(new SearchLocation("Hong Kong", "China", 22.3, 114.2));
        var today = DateTime.Now;
        var expected = SunCalc.CalculateSunriseSunset(22.3, 114.2, today.Year, today.Month, today.Day);
        int expectedLightMinutes = (expected.SunriseHour * 60) + expected.SunriseMinute;
        int expectedDarkMinutes = (expected.SunsetHour * 60) + expected.SunsetMinute;

        viewModel.InitializeScheduleMode();
        viewModel.ScheduleMode = viewModel.ScheduleMode;

        Assert.AreEqual(savedSettings, settings.ToJsonString());
        Assert.AreEqual<TimeSpan?>(TimeSpan.FromMinutes(expectedLightMinutes), viewModel.SunriseTimeSpan);
        Assert.AreEqual<TimeSpan?>(TimeSpan.FromMinutes(expectedDarkMinutes), viewModel.SunsetTimeSpan);
        Assert.AreEqual(TimeSpan.FromMinutes(expectedLightMinutes + 15), viewModel.LightTimeTimeSpan);
        Assert.AreEqual(TimeSpan.FromMinutes(expectedDarkMinutes - 20), viewModel.DarkTimeTimeSpan);
        Assert.AreEqual(-expectedLightMinutes, viewModel.SunriseOffsetMin);
        Assert.AreEqual(1439 - expectedDarkMinutes, viewModel.SunsetOffsetMax);
        Assert.AreEqual(TimeSpan.FromMinutes(503), viewModel.LightTimePickerValue);
        Assert.AreEqual(TimeSpan.FromMinutes(1259), viewModel.DarkTimePickerValue);
        Assert.AreEqual("Hong Kong", viewModel.SyncButtonInformation);
        Assert.AreEqual(0, messages.Count);

        var fixedSettings = (LightSwitchSettings)settings.Clone();
        fixedSettings.Properties.ScheduleMode.Value = "FixedHours";
        viewModel.ModuleSettings = fixedSettings;
        viewModel.InitializeScheduleMode();

        Assert.AreEqual(503, fixedSettings.Properties.LightTime.Value);
        Assert.AreEqual(1259, fixedSettings.Properties.DarkTime.Value);
        Assert.AreEqual(TimeSpan.FromMinutes(503), viewModel.LightTimeTimeSpan);
        Assert.AreEqual(TimeSpan.FromMinutes(1259), viewModel.DarkTimeTimeSpan);
        Assert.IsNull(viewModel.SunriseTimeSpan);
        Assert.IsNull(viewModel.SunsetTimeSpan);
        Assert.AreEqual(0, messages.Count);
    }

    [TestMethod]
    public void ModuleSettingsRefreshIgnoresTwoWayControlFeedback()
    {
        var messages = new List<string>();
        using var viewModel = CreateViewModel(new LightSwitchSettings(), message =>
        {
            messages.Add(message);
            return 0;
        });
        var profile = new PowerDisplayProfile("Night", new List<ProfileMonitorSetting>()) { Id = 7 };
        viewModel.AvailableProfiles.Add(profile);
        var settings = new LightSwitchSettings();
        settings.Properties.ScheduleMode.Value = "SunsetToSunrise";
        settings.Properties.LightTime.Value = 367;
        settings.Properties.DarkTime.Value = 1105;
        settings.Properties.SunriseOffset.Value = 15;
        settings.Properties.EnableDarkModeProfile.Value = true;
        settings.Properties.DarkModeProfileId.Value = profile.Id;
        string savedSettings = settings.ToJsonString();
        bool replayControlValues = true;
        viewModel.PropertyChanged += (_, e) =>
        {
            if (!replayControlValues)
            {
                return;
            }

            // Bound controls can report a clamped/previous value while receiving
            // new bounds, time values, mode or profile selection.
            switch (e.PropertyName)
            {
                case nameof(viewModel.SunriseOffsetMax):
                    viewModel.SunriseOffset = 0;
                    break;
                case nameof(viewModel.LightTimePickerValue):
                    viewModel.LightTimePickerValue = TimeSpan.FromHours(8);
                    break;
                case nameof(viewModel.DarkTimePickerValue):
                    viewModel.DarkTimePickerValue = TimeSpan.FromHours(20);
                    break;
                case nameof(viewModel.ScheduleMode):
                    viewModel.ScheduleMode = "FixedHours";
                    break;
                case nameof(viewModel.EnableDarkModeProfile):
                    viewModel.EnableDarkModeProfile = false;
                    break;
                case nameof(viewModel.SelectedDarkModeProfile):
                    viewModel.SelectedDarkModeProfile = null;
                    break;
            }
        };

        viewModel.ModuleSettings = settings;

        Assert.AreEqual(savedSettings, settings.ToJsonString());
        Assert.AreSame(profile, viewModel.SelectedDarkModeProfile);
        Assert.AreEqual(0, messages.Count);

        replayControlValues = false;
        viewModel.SunriseOffset = 16;
        Assert.AreEqual(16, settings.Properties.SunriseOffset.Value);
    }

    [TestMethod]
    public void SuppressedProfileSelectionChange_DoesNotPersistTemporaryZero()
    {
        var settings = new LightSwitchSettings();
        settings.Properties.DarkModeProfileId.Value = 7;
        var messages = new List<string>();
        var viewModel = CreateViewModel(settings, message =>
        {
            messages.Add(message);
            return 0;
        });
        var selected = new PowerDisplayProfile(
            "Night",
            new List<ProfileMonitorSetting>
            {
                new ProfileMonitorSetting("MON1", 50, null, null, null),
            })
        {
            Id = 7,
        };

        viewModel.SelectedDarkModeProfile = selected;
        SetSuppression(viewModel, true);
        viewModel.SelectedDarkModeProfile = null;

        Assert.AreEqual(7, settings.Properties.DarkModeProfileId.Value);
        Assert.AreEqual(0, messages.Count);
    }

    private static LightSwitchViewModel CreateViewModel(
        LightSwitchSettings settings,
        System.Func<string, int> sendConfigMessage)
    {
        var generalSettingsRepository =
            new BackCompatTestProperties.MockSettingsRepository<GeneralSettings>(
                ISettingsUtilsMocks.GetStubSettingsUtils<GeneralSettings>().Object);

        return new LightSwitchViewModel(
            generalSettingsRepository,
            settings,
            sendConfigMessage);
    }

    private static void AssertTimeBindingsNotified(HashSet<string> notifications)
    {
        string[] boundProperties =
        [
            nameof(LightSwitchViewModel.LightTimePickerValue),
            nameof(LightSwitchViewModel.DarkTimePickerValue),
            nameof(LightSwitchViewModel.LightTimeTimeSpan),
            nameof(LightSwitchViewModel.DarkTimeTimeSpan),
            nameof(LightSwitchViewModel.SunriseTimeSpan),
            nameof(LightSwitchViewModel.SunsetTimeSpan),
            nameof(LightSwitchViewModel.SunriseOffsetMin),
            nameof(LightSwitchViewModel.SunriseOffsetMax),
            nameof(LightSwitchViewModel.SunsetOffsetMin),
            nameof(LightSwitchViewModel.SunsetOffsetMax),
        ];
        foreach (string property in boundProperties)
        {
            Assert.IsTrue(notifications.Contains(property), $"Missing change notification for {property}.");
        }
    }

    private static void SetSuppression(LightSwitchViewModel viewModel, bool value)
    {
        var field = typeof(LightSwitchViewModel).GetField(
            "_suppressProfileSelectionPersistence",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field);
        field.SetValue(viewModel, value);
    }
}
