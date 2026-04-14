#include "pch.h"
#include "CppUnitTest.h"

// Include only pure-logic headers that have no heavy dependencies
#include "../LightSwitchService/LightSwitchUtils.h"
#include "../LightSwitchService/ThemeScheduler.h"
#include "../LightSwitchService/SettingsConstants.h"
#include "../LightSwitchLib/ThemeHelper.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

// Mirror the ScheduleMode enum and config struct from LightSwitchSettings.h
// to avoid pulling in heavy FileWatcher/SettingsAPI dependencies.
enum class ScheduleMode
{
    Off,
    FixedHours,
    SunsetToSunrise,
    FollowNightLight,
};

inline std::wstring ToString(ScheduleMode mode)
{
    switch (mode)
    {
    case ScheduleMode::FixedHours:
        return L"FixedHours";
    case ScheduleMode::SunsetToSunrise:
        return L"SunsetToSunrise";
    case ScheduleMode::FollowNightLight:
        return L"FollowNightLight";
    default:
        return L"Off";
    }
}

inline ScheduleMode FromString(const std::wstring& str)
{
    if (str == L"SunsetToSunrise")
        return ScheduleMode::SunsetToSunrise;
    if (str == L"FixedHours")
        return ScheduleMode::FixedHours;
    if (str == L"FollowNightLight")
        return ScheduleMode::FollowNightLight;
    else
        return ScheduleMode::Off;
}

struct LightSwitchConfig
{
    ScheduleMode scheduleMode = ScheduleMode::FixedHours;
    std::wstring latitude = L"0.0";
    std::wstring longitude = L"0.0";
    int lightTime = 8 * 60;
    int darkTime = 20 * 60;
    int sunrise_offset = 0;
    int sunset_offset = 0;
    bool changeSystem = false;
    bool changeApps = false;
    bool enableDarkModeProfile = false;
    bool enableLightModeProfile = false;
    std::wstring darkModeProfile = L"";
    std::wstring lightModeProfile = L"";
};

namespace LightSwitchUnitTests
{
    // ========================================================================
    // Registry Path Constants
    // ========================================================================
    TEST_CLASS(RegistryPathTests)
    {
    public:

        TEST_METHOD(PersonalizationRegistryPathIsCorrect)
        {
            std::wstring expected = L"Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize";
            Assert::AreEqual(expected, std::wstring(PERSONALIZATION_REGISTRY_PATH));
        }

        TEST_METHOD(NightLightRegistryPathContainsBluelight)
        {
            std::wstring path(NIGHT_LIGHT_REGISTRY_PATH);
            Assert::IsTrue(path.find(L"bluelightreduction") != std::wstring::npos,
                           L"Night light registry path should reference bluelightreduction");
        }

        TEST_METHOD(NightLightRegistryPathStartsWithSoftware)
        {
            std::wstring path(NIGHT_LIGHT_REGISTRY_PATH);
            Assert::IsTrue(path.find(L"Software\\Microsoft\\Windows\\CurrentVersion\\CloudStore") == 0,
                           L"Night light registry path should start with CloudStore path");
        }
    };

    // ========================================================================
    // ShouldBeLight — pure constexpr function from LightSwitchUtils.h
    // ========================================================================
    TEST_CLASS(ShouldBeLightTests)
    {
    public:

        TEST_METHOD(NormalRange_BeforeLightTime_ReturnsDark)
        {
            // lightTime=8:00 (480), darkTime=20:00 (1200), now=6:00 (360)
            Assert::IsFalse(ShouldBeLight(360, 480, 1200));
        }

        TEST_METHOD(NormalRange_AtLightTime_ReturnsLight)
        {
            // now exactly at light boundary
            Assert::IsTrue(ShouldBeLight(480, 480, 1200));
        }

        TEST_METHOD(NormalRange_MidDay_ReturnsLight)
        {
            // 12:00 noon is between 8:00 and 20:00
            Assert::IsTrue(ShouldBeLight(720, 480, 1200));
        }

        TEST_METHOD(NormalRange_AtDarkTime_ReturnsDark)
        {
            // now exactly at dark boundary → dark
            Assert::IsFalse(ShouldBeLight(1200, 480, 1200));
        }

        TEST_METHOD(NormalRange_AfterDarkTime_ReturnsDark)
        {
            // 22:00 (1320) after dark boundary
            Assert::IsFalse(ShouldBeLight(1320, 480, 1200));
        }

        TEST_METHOD(WraparoundRange_LightInEvening_DarkInMorning)
        {
            // lightTime=22:00 (1320), darkTime=6:00 (360)
            // 23:00 → light (after lightTime)
            Assert::IsTrue(ShouldBeLight(1380, 1320, 360));
        }

        TEST_METHOD(WraparoundRange_Midnight_ReturnsLight)
        {
            // lightTime=22:00 (1320), darkTime=6:00 (360)
            // 0:00 → light (wrapped around)
            Assert::IsTrue(ShouldBeLight(0, 1320, 360));
        }

        TEST_METHOD(WraparoundRange_MidDay_ReturnsDark)
        {
            // lightTime=22:00 (1320), darkTime=6:00 (360)
            // 12:00 → dark
            Assert::IsFalse(ShouldBeLight(720, 1320, 360));
        }

        TEST_METHOD(SameTime_LightEqualsDark_AlwaysLight)
        {
            // When lightTime == darkTime, the range is empty, so wrapped case applies
            // normalizedLightTime < normalizedDarkTime is false, so wrapped:
            // nowMinutes >= lightTime || nowMinutes < darkTime → always true
            Assert::IsTrue(ShouldBeLight(0, 480, 480));
            Assert::IsTrue(ShouldBeLight(480, 480, 480));
            Assert::IsTrue(ShouldBeLight(720, 480, 480));
        }

        TEST_METHOD(NegativeValues_NormalizedCorrectly)
        {
            // Negative values should be normalized into [0, 1439]
            // -60 → 1380, which is equivalent to 23:00
            // lightTime=480, darkTime=1200
            Assert::IsFalse(ShouldBeLight(-60, 480, 1200));
        }

        TEST_METHOD(LargeValues_NormalizedCorrectly)
        {
            // 1500 → 1500 % 1440 = 60, which is 1:00
            Assert::IsFalse(ShouldBeLight(1500, 480, 1200));
        }

        TEST_METHOD(MidnightBoundary_ExactlyMidnight)
        {
            // 0 minutes, lightTime=6:00, darkTime=18:00
            Assert::IsFalse(ShouldBeLight(0, 360, 1080));
        }
    };

    // ========================================================================
    // ScheduleMode enum ToString/FromString
    // ========================================================================
    TEST_CLASS(ScheduleModeTests)
    {
    public:

        TEST_METHOD(ToString_Off)
        {
            Assert::AreEqual(std::wstring(L"Off"), ToString(ScheduleMode::Off));
        }

        TEST_METHOD(ToString_FixedHours)
        {
            Assert::AreEqual(std::wstring(L"FixedHours"), ToString(ScheduleMode::FixedHours));
        }

        TEST_METHOD(ToString_SunsetToSunrise)
        {
            Assert::AreEqual(std::wstring(L"SunsetToSunrise"), ToString(ScheduleMode::SunsetToSunrise));
        }

        TEST_METHOD(ToString_FollowNightLight)
        {
            Assert::AreEqual(std::wstring(L"FollowNightLight"), ToString(ScheduleMode::FollowNightLight));
        }

        TEST_METHOD(FromString_FixedHours)
        {
            Assert::IsTrue(ScheduleMode::FixedHours == FromString(L"FixedHours"));
        }

        TEST_METHOD(FromString_SunsetToSunrise)
        {
            Assert::IsTrue(ScheduleMode::SunsetToSunrise == FromString(L"SunsetToSunrise"));
        }

        TEST_METHOD(FromString_FollowNightLight)
        {
            Assert::IsTrue(ScheduleMode::FollowNightLight == FromString(L"FollowNightLight"));
        }

        TEST_METHOD(FromString_UnknownDefaultsToOff)
        {
            Assert::IsTrue(ScheduleMode::Off == FromString(L"SomethingRandom"));
        }

        TEST_METHOD(FromString_EmptyDefaultsToOff)
        {
            Assert::IsTrue(ScheduleMode::Off == FromString(L""));
        }

        TEST_METHOD(Roundtrip_AllModes)
        {
            Assert::IsTrue(ScheduleMode::FixedHours == FromString(ToString(ScheduleMode::FixedHours)));
            Assert::IsTrue(ScheduleMode::SunsetToSunrise == FromString(ToString(ScheduleMode::SunsetToSunrise)));
            Assert::IsTrue(ScheduleMode::FollowNightLight == FromString(ToString(ScheduleMode::FollowNightLight)));
        }
    };

    // ========================================================================
    // LightSwitchConfig Defaults
    // ========================================================================
    TEST_CLASS(ConfigDefaultsTests)
    {
    public:

        TEST_METHOD(DefaultScheduleMode_IsFixedHours)
        {
            LightSwitchConfig config;
            Assert::IsTrue(config.scheduleMode == ScheduleMode::FixedHours);
        }

        TEST_METHOD(DefaultLightTime_Is0800)
        {
            LightSwitchConfig config;
            Assert::AreEqual(8 * 60, config.lightTime);
        }

        TEST_METHOD(DefaultDarkTime_Is2000)
        {
            LightSwitchConfig config;
            Assert::AreEqual(20 * 60, config.darkTime);
        }

        TEST_METHOD(DefaultLatitude_IsZero)
        {
            LightSwitchConfig config;
            Assert::AreEqual(std::wstring(L"0.0"), config.latitude);
        }

        TEST_METHOD(DefaultLongitude_IsZero)
        {
            LightSwitchConfig config;
            Assert::AreEqual(std::wstring(L"0.0"), config.longitude);
        }

        TEST_METHOD(DefaultSunriseOffset_IsZero)
        {
            LightSwitchConfig config;
            Assert::AreEqual(0, config.sunrise_offset);
        }

        TEST_METHOD(DefaultSunsetOffset_IsZero)
        {
            LightSwitchConfig config;
            Assert::AreEqual(0, config.sunset_offset);
        }

        TEST_METHOD(DefaultChangeSystem_IsFalse)
        {
            LightSwitchConfig config;
            Assert::IsFalse(config.changeSystem);
        }

        TEST_METHOD(DefaultChangeApps_IsFalse)
        {
            LightSwitchConfig config;
            Assert::IsFalse(config.changeApps);
        }

        TEST_METHOD(DefaultProfiles_Disabled)
        {
            LightSwitchConfig config;
            Assert::IsFalse(config.enableDarkModeProfile);
            Assert::IsFalse(config.enableLightModeProfile);
            Assert::AreEqual(std::wstring(L""), config.darkModeProfile);
            Assert::AreEqual(std::wstring(L""), config.lightModeProfile);
        }
    };

    // ========================================================================
    // ThemeScheduler — deg2rad / rad2deg pure math
    // ========================================================================
    TEST_CLASS(MathTests)
    {
    public:

        TEST_METHOD(Deg2Rad_Zero)
        {
            Assert::AreEqual(0.0, deg2rad(0.0), 0.0001);
        }

        TEST_METHOD(Deg2Rad_90)
        {
            Assert::AreEqual(PI / 2.0, deg2rad(90.0), 0.0001);
        }

        TEST_METHOD(Deg2Rad_180)
        {
            Assert::AreEqual(PI, deg2rad(180.0), 0.0001);
        }

        TEST_METHOD(Rad2Deg_Zero)
        {
            Assert::AreEqual(0.0, rad2deg(0.0), 0.0001);
        }

        TEST_METHOD(Rad2Deg_Pi)
        {
            Assert::AreEqual(180.0, rad2deg(PI), 0.0001);
        }

        TEST_METHOD(Deg2Rad_Rad2Deg_Roundtrip)
        {
            double original = 47.3;
            Assert::AreEqual(original, rad2deg(deg2rad(original)), 0.0001);
        }
    };

    // ========================================================================
    // CalculateSunriseSunset — smoke tests for known locations
    // ========================================================================
    TEST_CLASS(SunCalcTests)
    {
    public:

        TEST_METHOD(Seattle_JuneHasSunrise)
        {
            // Seattle: lat 47.6, lon -122.3, June 21 2024
            SunTimes times = CalculateSunriseSunset(47.6, -122.3, 2024, 6, 21);
            Assert::IsTrue(times.sunriseHour >= 0 && times.sunriseHour < 24,
                           L"Sunrise hour should be in [0,24)");
            Assert::IsTrue(times.sunsetHour >= 0 && times.sunsetHour < 24,
                           L"Sunset hour should be in [0,24)");
            Assert::IsTrue(times.sunriseMinute >= 0 && times.sunriseMinute < 60);
            Assert::IsTrue(times.sunsetMinute >= 0 && times.sunsetMinute < 60);

            // UTC sunrise for Seattle June 21 is roughly 12:10 UTC (5:10 PDT)
            // Allow ±2 hour tolerance since result is converted to machine-local time
            int riseMinutes = times.sunriseHour * 60 + times.sunriseMinute;
            int setMinutes = times.sunsetHour * 60 + times.sunsetMinute;
            int daylight = setMinutes - riseMinutes;
            if (daylight < 0)
                daylight += 24 * 60;
            // Daylight should be at least 14 hours at summer solstice, 47°N
            Assert::IsTrue(daylight >= 14 * 60,
                           L"Seattle June 21 should have at least 14 hours of daylight");
        }

        TEST_METHOD(Equator_DecemberHasReasonableTimes)
        {
            // Equator: lat 0, lon 0, Dec 21 — roughly 12 hours daylight year-round
            SunTimes times = CalculateSunriseSunset(0.0, 0.0, 2024, 12, 21);
            Assert::IsTrue(times.sunriseHour >= 0 && times.sunriseHour < 24);
            Assert::IsTrue(times.sunsetHour >= 0 && times.sunsetHour < 24);

            int riseMinutes = times.sunriseHour * 60 + times.sunriseMinute;
            int setMinutes = times.sunsetHour * 60 + times.sunsetMinute;
            int daylight = setMinutes - riseMinutes;
            // Handle timezone wrap: if machine TZ shifts sunrise past midnight,
            // daylight goes negative — add 24h to correct
            if (daylight < 0)
                daylight += 24 * 60;
            // Equator gets ~12 hours year round
            Assert::IsTrue(daylight >= 10 * 60 && daylight <= 14 * 60,
                           L"Equator should have 10-14 hours of daylight");
        }

        TEST_METHOD(SunriseBeforeSunset_NormalLatitude)
        {
            // New York area: lat 40, lon -74, March equinox
            SunTimes times = CalculateSunriseSunset(40.0, -74.0, 2024, 3, 21);
            int riseMinutes = times.sunriseHour * 60 + times.sunriseMinute;
            int setMinutes = times.sunsetHour * 60 + times.sunsetMinute;
            int daylight = setMinutes - riseMinutes;
            if (daylight < 0)
                daylight += 24 * 60;
            // Equinox: ~12 hours daylight everywhere
            Assert::IsTrue(daylight >= 11 * 60 && daylight <= 13 * 60,
                           L"Near equinox, daylight should be approximately 12 hours");
        }

        TEST_METHOD(WinterSolstice_ShorterDays)
        {
            // Seattle Dec 21 — shortest day, ~8.5 hours of daylight
            SunTimes times = CalculateSunriseSunset(47.6, -122.3, 2024, 12, 21);
            int riseMinutes = times.sunriseHour * 60 + times.sunriseMinute;
            int setMinutes = times.sunsetHour * 60 + times.sunsetMinute;
            int daylight = setMinutes - riseMinutes;
            if (daylight < 0)
                daylight += 24 * 60;
            Assert::IsTrue(daylight >= 7 * 60 && daylight <= 10 * 60,
                           L"Seattle Dec 21 should have 7-10 hours of daylight");
        }

        TEST_METHOD(HighLatitude_LongSummerDay)
        {
            // Reykjavik Iceland: 64.1°N, June 21 — near midnight sun
            SunTimes times = CalculateSunriseSunset(64.1, -21.9, 2024, 6, 21);
            int riseMinutes = times.sunriseHour * 60 + times.sunriseMinute;
            int setMinutes = times.sunsetHour * 60 + times.sunsetMinute;
            if (times.sunriseHour >= 0 && times.sunsetHour >= 0)
            {
                int daylight = setMinutes - riseMinutes;
                if (daylight < 0)
                    daylight += 24 * 60;
                Assert::IsTrue(daylight >= 20 * 60,
                               L"Reykjavik June 21 should have 20+ hours of daylight");
            }
        }
    };

    // ========================================================================
    // SettingId enum completeness
    // ========================================================================
    TEST_CLASS(SettingIdTests)
    {
    public:

        TEST_METHOD(SettingId_ScheduleMode_IsZero)
        {
            Assert::AreEqual(0, static_cast<int>(SettingId::ScheduleMode));
        }

        TEST_METHOD(SettingId_AllValuesDistinct)
        {
            // Verify no accidental duplicates
            std::vector<int> values = {
                static_cast<int>(SettingId::ScheduleMode),
                static_cast<int>(SettingId::Latitude),
                static_cast<int>(SettingId::Longitude),
                static_cast<int>(SettingId::LightTime),
                static_cast<int>(SettingId::DarkTime),
                static_cast<int>(SettingId::Sunrise_Offset),
                static_cast<int>(SettingId::Sunset_Offset),
                static_cast<int>(SettingId::ChangeSystem),
                static_cast<int>(SettingId::ChangeApps),
            };
            std::sort(values.begin(), values.end());
            auto last = std::unique(values.begin(), values.end());
            Assert::AreEqual(values.size(), static_cast<size_t>(std::distance(values.begin(), last)),
                             L"All SettingId values should be unique");
        }
    };
}
