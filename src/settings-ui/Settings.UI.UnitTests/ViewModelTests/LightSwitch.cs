// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
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
    [DataRow(-61)]
    [DataRow(-1)]
    [DataRow(1440)]
    [DataRow(int.MinValue)]
    [DataRow(int.MaxValue)]
    public void InvalidStoredTimesRefreshAsUnsetPickersWithoutChangingTheSettings(int minutes)
    {
        var messages = new List<string>();
        using var viewModel = CreateViewModel(new LightSwitchSettings(), message =>
        {
            messages.Add(message);
            return 0;
        });
        var notifications = new HashSet<string>();
        viewModel.PropertyChanged += (_, e) =>
        {
            notifications.Add(e.PropertyName);
            if (e.PropertyName == nameof(viewModel.LightTimePickerValue))
            {
                Assert.AreEqual(TimeSpan.FromTicks(-1), viewModel.LightTimePickerValue);
                viewModel.LightTimePickerValue = viewModel.LightTimePickerValue;
            }
            else if (e.PropertyName == nameof(viewModel.DarkTimePickerValue))
            {
                Assert.AreEqual(TimeSpan.FromTicks(-1), viewModel.DarkTimePickerValue);
                viewModel.DarkTimePickerValue = viewModel.DarkTimePickerValue;
            }
        };
        var settings = CreateSunSettings(minutes, minutes, 0, 0);
        string original = settings.ToJsonString();

        viewModel.ModuleSettings = settings;

        Assert.IsTrue(notifications.Contains(nameof(viewModel.LightTimePickerValue)));
        Assert.IsTrue(notifications.Contains(nameof(viewModel.DarkTimePickerValue)));
        Assert.AreEqual(original, settings.ToJsonString());
        Assert.AreEqual(0, messages.Count);
    }

    [TestMethod]
    [DataRow(-61, -61)]
    [DataRow(-61, 1080)]
    [DataRow(360, -61)]
    public void ApplyingLocationWithMissingSunTimesPreservesRawValuesAndUsesSafePickers(int lightMinutes, int darkMinutes)
    {
        var messages = new List<string>();
        using var viewModel = CreateViewModel(CreateSunSettings(360, 1080, 15, -20), message =>
        {
            messages.Add(message);
            return 0;
        });
        viewModel.PropertyChanged += (_, e) =>
        {
            // Replay the generated TwoWay feedback while refreshing both pickers.
            if (e.PropertyName == nameof(viewModel.LightTimePickerValue))
            {
                viewModel.LightTimePickerValue = viewModel.LightTimePickerValue;
            }
            else if (e.PropertyName == nameof(viewModel.DarkTimePickerValue))
            {
                viewModel.DarkTimePickerValue = viewModel.DarkTimePickerValue;
            }
        };

        viewModel.ApplyLocation(85, 0, lightMinutes, darkMinutes);

        Assert.AreEqual(lightMinutes, viewModel.LightTime);
        Assert.AreEqual(darkMinutes, viewModel.DarkTime);
        Assert.AreEqual(lightMinutes < 0 ? TimeSpan.FromTicks(-1) : TimeSpan.FromMinutes(lightMinutes), viewModel.LightTimePickerValue);
        Assert.AreEqual(darkMinutes < 0 ? TimeSpan.FromTicks(-1) : TimeSpan.FromMinutes(darkMinutes), viewModel.DarkTimePickerValue);
        Assert.AreEqual(15, viewModel.SunriseOffset);
        Assert.AreEqual(-20, viewModel.SunsetOffset);
        AssertCompleteSavedSettings(viewModel, messages);
    }

    [TestMethod]
    public void UnsetAndInvalidPickerFeedbackDoesNotWriteMidnightOrNotifyASave()
    {
        using var viewModel = CreateViewModel(CreateSunSettings(-61, 1080, 0, 0), _ => 0);
        int notifications = 0;
        viewModel.PropertyChanged += (_, _) => ++notifications;

        foreach (var invalid in new[]
        {
            TimeSpan.FromTicks(-1),
            TimeSpan.FromMinutes(-61),
            TimeSpan.FromDays(1),
            TimeSpan.FromHours(25),
            TimeSpan.MinValue,
            TimeSpan.MaxValue,
        })
        {
            viewModel.LightTimePickerValue = invalid;
            viewModel.DarkTimePickerValue = invalid;
        }

        Assert.AreEqual(-61, viewModel.LightTime);
        Assert.AreEqual(1080, viewModel.DarkTime);
        Assert.AreEqual(0, notifications, "Rejected picker feedback must not trigger the page's settings-save handler.");
    }

    [TestMethod]
    [DataRow(0, 1439)]
    [DataRow(437, 1259)]
    public void ValidPickerEditsIncludeMidnightAndTheLastMinuteOfTheDay(int lightMinutes, int darkMinutes)
    {
        var settings = new LightSwitchSettings();
        settings.Properties.ScheduleMode.Value = "FixedHours";
        using var viewModel = CreateViewModel(settings, _ => 0);
        var notifications = new HashSet<string>();
        viewModel.PropertyChanged += (_, e) => notifications.Add(e.PropertyName);

        viewModel.LightTimePickerValue = TimeSpan.FromMinutes(lightMinutes);
        viewModel.DarkTimePickerValue = TimeSpan.FromMinutes(darkMinutes);

        Assert.AreEqual(lightMinutes, settings.Properties.LightTime.Value);
        Assert.AreEqual(darkMinutes, settings.Properties.DarkTime.Value);
        Assert.AreEqual(TimeSpan.FromMinutes(lightMinutes), viewModel.LightTimePickerValue);
        Assert.AreEqual(TimeSpan.FromMinutes(darkMinutes), viewModel.DarkTimePickerValue);
        Assert.IsTrue(notifications.Contains(nameof(viewModel.LightTime)));
        Assert.IsTrue(notifications.Contains(nameof(viewModel.DarkTime)));
    }

    [TestMethod]
    public void ApplyingLocationPreservesValidOffsetsAndPublishesOneCompleteSnapshot()
    {
        var settings = CreateSunSettings(60, 180, 30, 0);
        var messages = new List<string>();
        using var viewModel = CreateViewModel(settings, message =>
        {
            messages.Add(message);
            return 0;
        });
        viewModel.RefreshModuleSettings();
        using var controls = new OffsetControlFeedback(viewModel);
        int notifications = 0;
        int pageSaves = 0;
        viewModel.PropertyChanged += (_, _) =>
        {
            ++notifications;

            // The page suppresses persistence while a complete source snapshot is
            // being published to bindings; the location operation saves once later.
            if (!viewModel.IsRefreshingModuleSettings)
            {
                ++pageSaves;
            }

            Assert.AreEqual("22.3", viewModel.Latitude);
            Assert.AreEqual("114.2", viewModel.Longitude);
            Assert.AreEqual(480, viewModel.LightTime);
            Assert.AreEqual(1080, viewModel.DarkTime);
            Assert.AreEqual<TimeSpan?>(TimeSpan.FromMinutes(480), viewModel.SunriseTimeSpan);
            Assert.AreEqual<TimeSpan?>(TimeSpan.FromMinutes(1080), viewModel.SunsetTimeSpan);
        };

        viewModel.ApplyLocation(22.3, 114.2, 480, 1080);

        Assert.IsTrue(notifications > 0);
        Assert.AreEqual(0, pageSaves);
        Assert.AreEqual(30, viewModel.SunriseOffset);
        Assert.AreEqual(30, controls.SunriseValue);
        Assert.AreEqual(0, viewModel.SunsetOffset);
        AssertCompleteSavedSettings(viewModel, messages);

        viewModel.ApplyLocation(22.3, 114.2, 480, 1080);
        Assert.AreEqual(1, messages.Count, "An unchanged location must not send another settings save.");
    }

    [TestMethod]
    [DataRow(-500, 0, 60, 120, -60, 0)]
    [DataRow(500, 100, 600, 660, 159, 100)]
    [DataRow(0, 100, 480, 1400, 0, 39)]
    public void ApplyingLocationClampsOffsetsAgainstTheCompleteFinalRange(
        int oldSunriseOffset, int oldSunsetOffset, int lightMinutes, int darkMinutes, int expectedSunriseOffset, int expectedSunsetOffset)
    {
        var messages = new List<string>();
        using var viewModel = CreateViewModel(CreateSunSettings(600, 1200, oldSunriseOffset, oldSunsetOffset), message =>
        {
            messages.Add(message);
            return 0;
        });
        viewModel.RefreshModuleSettings();
        using var controls = new OffsetControlFeedback(viewModel);

        viewModel.ApplyLocation(22.3, 114.2, lightMinutes, darkMinutes);

        Assert.AreEqual(expectedSunriseOffset, viewModel.SunriseOffset);
        Assert.AreEqual(expectedSunsetOffset, viewModel.SunsetOffset);
        Assert.AreEqual(expectedSunriseOffset, controls.SunriseValue);
        Assert.AreEqual(expectedSunsetOffset, controls.SunsetValue);
        Assert.IsTrue(viewModel.SunriseOffset >= viewModel.SunriseOffsetMin && viewModel.SunriseOffset <= viewModel.SunriseOffsetMax);
        Assert.IsTrue(viewModel.SunsetOffset >= viewModel.SunsetOffsetMin && viewModel.SunsetOffset <= viewModel.SunsetOffsetMax);
        AssertCompleteSavedSettings(viewModel, messages);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ManualAndSelectedCityLocationsUseTheSameAtomicApplication(bool selectCity)
    {
        var messages = new List<string>();
        using var viewModel = CreateViewModel(CreateSunSettings(60, 180, 30, 0), message =>
        {
            messages.Add(message);
            return 0;
        });

        // Keep solar noon near local noon in every test machine's time zone.
        // Near the equator the new sunrise stays later than the old 03:00 sunset.
        var today = DateTime.Now;
        double longitude = (((TimeZoneInfo.Local.GetUtcOffset(today).TotalHours * 15) + 540) % 360) - 180;
        var city = new SearchLocation("Test city", "Test country", 1, longitude);
        viewModel.SearchLocations.Add(city);
        viewModel.RefreshModuleSettings();
        using var controls = new OffsetControlFeedback(viewModel);
        var expected = SunCalc.CalculateSunriseSunset(city.Latitude, city.Longitude, today.Year, today.Month, today.Day);
        int lightMinutes = (expected.SunriseHour * 60) + expected.SunriseMinute;
        int darkMinutes = (expected.SunsetHour * 60) + expected.SunsetMinute;
        int pageSaves = 0;
        viewModel.PropertyChanged += (_, _) =>
        {
            if (!viewModel.IsRefreshingModuleSettings)
            {
                ++pageSaves;
            }

            Assert.AreEqual(lightMinutes, viewModel.LightTime);
            Assert.AreEqual(darkMinutes, viewModel.DarkTime);
        };

        if (selectCity)
        {
            viewModel.SelectedCity = city;
        }
        else
        {
            // This is the same entry point used by LocationDialog_PrimaryButtonClick.
            viewModel.UpdateSunTimes(city.Latitude, city.Longitude);
        }

        Assert.AreEqual(0, pageSaves);
        Assert.AreSame(city, viewModel.SelectedCity);
        Assert.AreEqual(city.Latitude.ToString(CultureInfo.InvariantCulture), viewModel.Latitude);
        Assert.AreEqual(city.Longitude.ToString(CultureInfo.InvariantCulture), viewModel.Longitude);
        Assert.AreEqual(30, viewModel.SunriseOffset);
        Assert.AreEqual(30, controls.SunriseValue);
        AssertCompleteSavedSettings(viewModel, messages);
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

    private static LightSwitchSettings CreateSunSettings(int lightMinutes, int darkMinutes, int sunriseOffset, int sunsetOffset)
    {
        var settings = new LightSwitchSettings();
        settings.Properties.ScheduleMode.Value = "SunsetToSunrise";
        settings.Properties.LightTime.Value = lightMinutes;
        settings.Properties.DarkTime.Value = darkMinutes;
        settings.Properties.SunriseOffset.Value = sunriseOffset;
        settings.Properties.SunsetOffset.Value = sunsetOffset;
        return settings;
    }

    private static void AssertCompleteSavedSettings(LightSwitchViewModel viewModel, List<string> messages)
    {
        Assert.AreEqual(1, messages.Count);
        using var document = JsonDocument.Parse(messages[0]);
        var saved = JsonSerializer.Deserialize<LightSwitchSettings>(document.RootElement.GetProperty("powertoys").GetProperty("LightSwitch"));
        Assert.IsNotNull(saved);
        Assert.AreEqual(viewModel.ModuleSettings.ToJsonString(), saved.ToJsonString());
    }

    // Models the generated x:Bind maximum/minimum/value assignments and synchronous
    // NumberBox coercion callbacks while executing the production ViewModel unchanged.
    private sealed class OffsetControlFeedback : IDisposable
    {
        private readonly LightSwitchViewModel viewModel;
        private readonly OffsetControl sunrise;
        private readonly OffsetControl sunset;

        public OffsetControlFeedback(LightSwitchViewModel viewModel)
        {
            this.viewModel = viewModel;
            sunrise = new OffsetControl(viewModel.SunriseOffsetMin, viewModel.SunriseOffsetMax, viewModel.SunriseOffset, value => viewModel.SunriseOffset = value);
            sunset = new OffsetControl(viewModel.SunsetOffsetMin, viewModel.SunsetOffsetMax, viewModel.SunsetOffset, value => viewModel.SunsetOffset = value);
            viewModel.PropertyChanged += OnPropertyChanged;
        }

        public int SunriseValue => sunrise.Value;

        public int SunsetValue => sunset.Value;

        public void Dispose() => viewModel.PropertyChanged -= OnPropertyChanged;

        private void OnPropertyChanged(object sender, PropertyChangedEventArgs args)
        {
            switch (args.PropertyName)
            {
                case nameof(viewModel.SunriseOffsetMin):
                    sunrise.SetMinimum(viewModel.SunriseOffsetMin);
                    break;
                case nameof(viewModel.SunriseOffsetMax):
                    sunrise.SetMaximum(viewModel.SunriseOffsetMax);
                    break;
                case nameof(viewModel.SunriseOffset):
                    sunrise.SetValue(viewModel.SunriseOffset);
                    break;
                case nameof(viewModel.SunsetOffsetMin):
                    sunset.SetMinimum(viewModel.SunsetOffsetMin);
                    break;
                case nameof(viewModel.SunsetOffsetMax):
                    sunset.SetMaximum(viewModel.SunsetOffsetMax);
                    break;
                case nameof(viewModel.SunsetOffset):
                    sunset.SetValue(viewModel.SunsetOffset);
                    break;
            }
        }

        private sealed class OffsetControl(int initialMinimum, int initialMaximum, int initialValue, Action<int> writeBack)
        {
            private int minimum = initialMinimum;
            private int maximum = initialMaximum;

            public int Value { get; private set; } = initialValue;

            public void SetMinimum(int value)
            {
                minimum = value;
                maximum = Math.Max(maximum, minimum);
                SetValue(Value);
            }

            public void SetMaximum(int value)
            {
                maximum = value;
                minimum = Math.Min(minimum, maximum);
                SetValue(Value);
            }

            public void SetValue(int value)
            {
                int previous = Value;
                Value = Math.Clamp(value, minimum, maximum);
                if (Value != previous)
                {
                    writeBack(Value);
                }
            }
        }
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
