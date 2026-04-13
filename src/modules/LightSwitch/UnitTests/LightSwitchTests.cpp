#include "pch.h"
#include "CppUnitTest.h"

// Shared lightweight types — the REAL product definitions of ScheduleMode,
// ToString/FromString, and LightSwitchConfig. No heavy dependencies.
#include "../LightSwitchTypes.h"

// Pure-logic product headers with no heavy dependencies
#include "../LightSwitchService/LightSwitchUtils.h"
#include "../LightSwitchService/ThemeScheduler.h"
#include "../LightSwitchService/SettingsConstants.h"
#include "../LightSwitchLib/ThemeHelper.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace LightSwitchUnitTests
{
    // ========================================================================
    // Registry Path Constants
    // ========================================================================
    TEST_CLASS(RegistryPathTests)
    {
    public:

        // Product code: ThemeHelper.h — PERSONALIZATION_REGISTRY_PATH constant
        // What: Verifies the registry path matches the Windows personalization key
        // Why: Wrong path = theme reads/writes silently target the wrong key
        // Risk: LightSwitch stops detecting or changing system/app themes entirely
        TEST_METHOD(PersonalizationRegistryPathIsCorrect)
        {
            std::wstring expected = L"Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize";
            Assert::AreEqual(expected, std::wstring(PERSONALIZATION_REGISTRY_PATH));
        }

        // Product code: ThemeHelper.h — NIGHT_LIGHT_REGISTRY_PATH constant
        // What: Verifies path contains "bluelightreduction" (Night Light's internal name)
        // Why: Night Light monitoring depends on this undocumented registry key
        // Risk: FollowNightLight mode becomes completely non-functional
        TEST_METHOD(NightLightRegistryPathContainsBluelight)
        {
            std::wstring path(NIGHT_LIGHT_REGISTRY_PATH);
            Assert::IsTrue(path.find(L"bluelightreduction") != std::wstring::npos,
                           L"Night light registry path should reference bluelightreduction");
        }

        // Product code: ThemeHelper.h — NIGHT_LIGHT_REGISTRY_PATH constant
        // What: Verifies path starts with the expected CloudStore prefix
        // Why: Night Light data lives under CloudStore; wrong prefix = no registry events
        // Risk: Night Light changes go undetected, theme stays stale
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

        // Product code: LightSwitchUtils.h — ShouldBeLight(currentMinutes, lightTime, darkTime)
        // What: Verifies that times before the light-time threshold return false (dark mode)
        // Why: Core scheduling logic — regression would cause wrong theme at wrong time
        // Risk: Users wake up to wrong theme; complaints about light blinding them at night
        TEST_METHOD(NormalRange_BeforeLightTime_ReturnsDark)
        {
            // lightTime=8:00 (480), darkTime=20:00 (1200), now=6:00 (360)
            Assert::IsFalse(ShouldBeLight(360, 480, 1200));
        }

        // Product code: LightSwitchUtils.h — ShouldBeLight(currentMinutes, lightTime, darkTime)
        // What: Verifies the exact light-time boundary is inclusive (light mode starts)
        // Why: Off-by-one here means the theme switches 1 minute late
        // Risk: Boundary mismatch with user-configured time; theme flickers at boundary
        TEST_METHOD(NormalRange_AtLightTime_ReturnsLight)
        {
            Assert::IsTrue(ShouldBeLight(480, 480, 1200));
        }

        // Product code: LightSwitchUtils.h — ShouldBeLight(currentMinutes, lightTime, darkTime)
        // What: Verifies midday (well within the light window) returns light
        // Why: Sanity check that the normal range works for typical daytime
        // Risk: Basic scheduling broken for the most common use case
        TEST_METHOD(NormalRange_MidDay_ReturnsLight)
        {
            Assert::IsTrue(ShouldBeLight(720, 480, 1200));
        }

        // Product code: LightSwitchUtils.h — ShouldBeLight(currentMinutes, lightTime, darkTime)
        // What: Verifies the exact dark-time boundary is exclusive (dark mode begins)
        // Why: Dark boundary must be exclusive so light window is [lightTime, darkTime)
        // Risk: Off-by-one causes dark mode to start 1 minute late
        TEST_METHOD(NormalRange_AtDarkTime_ReturnsDark)
        {
            Assert::IsFalse(ShouldBeLight(1200, 480, 1200));
        }

        // Product code: LightSwitchUtils.h — ShouldBeLight(currentMinutes, lightTime, darkTime)
        // What: Verifies times after dark boundary stay in dark mode
        // Why: Ensures the entire evening period is correctly classified
        // Risk: Late-evening users see wrong theme
        TEST_METHOD(NormalRange_AfterDarkTime_ReturnsDark)
        {
            Assert::IsFalse(ShouldBeLight(1320, 480, 1200));
        }

        // Product code: LightSwitchUtils.h — ShouldBeLight(currentMinutes, lightTime, darkTime)
        // What: Verifies wrap-around schedule (light starts evening, dark starts morning)
        // Why: Night-owl users configure light=22:00, dark=06:00; wrap logic must work
        // Risk: Wrap-around users get inverted themes (dark when expecting light)
        TEST_METHOD(WraparoundRange_LightInEvening_DarkInMorning)
        {
            // lightTime=22:00 (1320), darkTime=6:00 (360), now=23:00 → light
            Assert::IsTrue(ShouldBeLight(1380, 1320, 360));
        }

        // Product code: LightSwitchUtils.h — ShouldBeLight(currentMinutes, lightTime, darkTime)
        // What: Verifies midnight is in the light window for wrap-around schedules
        // Why: Midnight is the tricky wrap point — modular arithmetic must handle it
        // Risk: Theme flips incorrectly at midnight for wrap-around users
        TEST_METHOD(WraparoundRange_Midnight_ReturnsLight)
        {
            Assert::IsTrue(ShouldBeLight(0, 1320, 360));
        }

        // Product code: LightSwitchUtils.h — ShouldBeLight(currentMinutes, lightTime, darkTime)
        // What: Verifies midday returns dark for wrap-around (light is nighttime only)
        // Why: Ensures the dark window in a wrap-around schedule covers daytime
        // Risk: Wrap-around schedule logic inverted during the day
        TEST_METHOD(WraparoundRange_MidDay_ReturnsDark)
        {
            Assert::IsFalse(ShouldBeLight(720, 1320, 360));
        }

        // Product code: LightSwitchUtils.h — ShouldBeLight(currentMinutes, lightTime, darkTime)
        // What: Verifies that lightTime == darkTime means always-light (empty dark range)
        // Why: Edge case where user sets identical times; must not crash or flip randomly
        // Risk: Undefined behavior at degenerate boundary causes erratic theme switching
        TEST_METHOD(SameTime_LightEqualsDark_AlwaysLight)
        {
            Assert::IsTrue(ShouldBeLight(0, 480, 480));
            Assert::IsTrue(ShouldBeLight(480, 480, 480));
            Assert::IsTrue(ShouldBeLight(720, 480, 480));
        }

        // Product code: LightSwitchUtils.h — ShouldBeLight(currentMinutes, lightTime, darkTime)
        // What: Verifies negative minute values are normalized into [0, 1439]
        // Why: Offset arithmetic (sunrise_offset) can produce negative minutes
        // Risk: Negative input causes wrong modulo result or crash on some platforms
        TEST_METHOD(NegativeValues_NormalizedCorrectly)
        {
            // -60 normalizes to 1380 (23:00), outside light window [480, 1200) → dark
            Assert::IsFalse(ShouldBeLight(-60, 480, 1200));
        }

        // Product code: LightSwitchUtils.h — ShouldBeLight(currentMinutes, lightTime, darkTime)
        // What: Verifies values exceeding 1440 are normalized correctly
        // Why: Offset arithmetic can push minutes past 24:00 (1440)
        // Risk: Values > 1440 bypass the range check entirely
        TEST_METHOD(LargeValues_NormalizedCorrectly)
        {
            // 1500 normalizes to 60 (01:00), outside light window → dark
            Assert::IsFalse(ShouldBeLight(1500, 480, 1200));
        }

        // Product code: LightSwitchUtils.h — ShouldBeLight(currentMinutes, lightTime, darkTime)
        // What: Verifies midnight (0 minutes) is handled correctly
        // Why: Midnight is a common boundary; modulo edge at 0 must be correct
        // Risk: Off-by-one or signed/unsigned issue at the midnight boundary
        TEST_METHOD(MidnightBoundary_ExactlyMidnight)
        {
            Assert::IsFalse(ShouldBeLight(0, 360, 1080));
        }
    };

    // ========================================================================
    // ScheduleMode enum ToString/FromString — now testing REAL product code
    // from LightSwitchTypes.h (no longer copy-pasted duplicates)
    // ========================================================================
    TEST_CLASS(ScheduleModeTests)
    {
    public:

        // Product code: LightSwitchTypes.h — ToString(ScheduleMode)
        // What: Verifies Off mode serializes to "Off"
        // Why: Settings JSON uses this string; wrong value = settings fail to load
        // Risk: Users' schedule mode resets to default on every settings save
        TEST_METHOD(ToString_Off)
        {
            Assert::AreEqual(std::wstring(L"Off"), ToString(ScheduleMode::Off));
        }

        // Product code: LightSwitchTypes.h — ToString(ScheduleMode)
        // What: Verifies FixedHours serializes correctly
        // Why: Most common mode — broken serialization affects majority of users
        // Risk: FixedHours users silently fall back to Off mode
        TEST_METHOD(ToString_FixedHours)
        {
            Assert::AreEqual(std::wstring(L"FixedHours"), ToString(ScheduleMode::FixedHours));
        }

        // Product code: LightSwitchTypes.h — ToString(ScheduleMode)
        // What: Verifies SunsetToSunrise serializes correctly
        // Why: This mode string must match what the settings UI writes
        // Risk: Sunset/sunrise users lose their configuration on restart
        TEST_METHOD(ToString_SunsetToSunrise)
        {
            Assert::AreEqual(std::wstring(L"SunsetToSunrise"), ToString(ScheduleMode::SunsetToSunrise));
        }

        // Product code: LightSwitchTypes.h — ToString(ScheduleMode)
        // What: Verifies FollowNightLight serializes correctly
        // Why: Must match the string the settings UI writes to JSON
        // Risk: Night Light followers lose their mode on restart
        TEST_METHOD(ToString_FollowNightLight)
        {
            Assert::AreEqual(std::wstring(L"FollowNightLight"), ToString(ScheduleMode::FollowNightLight));
        }

        // Product code: LightSwitchTypes.h — FromString(wstring)
        // What: Verifies "FixedHours" deserializes back to the correct enum
        // Why: Settings loading depends on exact string matching
        // Risk: Loaded settings silently default to Off
        TEST_METHOD(FromString_FixedHours)
        {
            Assert::IsTrue(ScheduleMode::FixedHours == FromString(L"FixedHours"));
        }

        // Product code: LightSwitchTypes.h — FromString(wstring)
        // What: Verifies "SunsetToSunrise" deserializes correctly
        // Why: Case-sensitive match — any typo in the constant breaks this
        // Risk: SunsetToSunrise users fall back to Off silently
        TEST_METHOD(FromString_SunsetToSunrise)
        {
            Assert::IsTrue(ScheduleMode::SunsetToSunrise == FromString(L"SunsetToSunrise"));
        }

        // Product code: LightSwitchTypes.h — FromString(wstring)
        // What: Verifies "FollowNightLight" deserializes correctly
        // Why: Case-sensitive string match must be exact
        // Risk: Night Light mode users fall back to Off
        TEST_METHOD(FromString_FollowNightLight)
        {
            Assert::IsTrue(ScheduleMode::FollowNightLight == FromString(L"FollowNightLight"));
        }

        // Product code: LightSwitchTypes.h — FromString(wstring)
        // What: Verifies unknown strings default to Off (safe fallback)
        // Why: Corrupted or future-version settings must not crash
        // Risk: Unrecognized mode causes undefined behavior or crash
        TEST_METHOD(FromString_UnknownDefaultsToOff)
        {
            Assert::IsTrue(ScheduleMode::Off == FromString(L"SomethingRandom"));
        }

        // Product code: LightSwitchTypes.h — FromString(wstring)
        // What: Verifies empty string defaults to Off
        // Why: Missing "scheduleMode" key in JSON yields empty string
        // Risk: Empty string causes crash or unexpected mode selection
        TEST_METHOD(FromString_EmptyDefaultsToOff)
        {
            Assert::IsTrue(ScheduleMode::Off == FromString(L""));
        }

        // Product code: LightSwitchTypes.h — ToString + FromString roundtrip
        // What: Verifies serialization/deserialization is lossless for all modes
        // Why: Settings are saved then loaded — roundtrip must be identity
        // Risk: Mode silently changes between save and load cycles
        TEST_METHOD(Roundtrip_AllModes)
        {
            Assert::IsTrue(ScheduleMode::FixedHours == FromString(ToString(ScheduleMode::FixedHours)));
            Assert::IsTrue(ScheduleMode::SunsetToSunrise == FromString(ToString(ScheduleMode::SunsetToSunrise)));
            Assert::IsTrue(ScheduleMode::FollowNightLight == FromString(ToString(ScheduleMode::FollowNightLight)));
        }
    };

    // ========================================================================
    // LightSwitchConfig Defaults — now testing REAL product struct
    // from LightSwitchTypes.h (no longer copy-pasted duplicates)
    // ========================================================================
    TEST_CLASS(ConfigDefaultsTests)
    {
    public:

        // Product code: LightSwitchTypes.h — LightSwitchConfig::scheduleMode default
        // What: Verifies new configs default to FixedHours
        // Why: First-run experience depends on a sensible default mode
        // Risk: New users start with Off mode and think LightSwitch is broken
        TEST_METHOD(DefaultScheduleMode_IsFixedHours)
        {
            LightSwitchConfig config;
            Assert::IsTrue(config.scheduleMode == ScheduleMode::FixedHours);
        }

        // Product code: LightSwitchTypes.h — LightSwitchConfig::lightTime default
        // What: Verifies light time defaults to 08:00 (480 minutes)
        // Why: Default must be a reasonable morning time for first-run
        // Risk: Users get light mode at an unexpected hour on first launch
        TEST_METHOD(DefaultLightTime_Is0800)
        {
            LightSwitchConfig config;
            Assert::AreEqual(8 * 60, config.lightTime);
        }

        // Product code: LightSwitchTypes.h — LightSwitchConfig::darkTime default
        // What: Verifies dark time defaults to 20:00 (1200 minutes)
        // Why: Default must be a reasonable evening time for first-run
        // Risk: Users get dark mode during the workday on first launch
        TEST_METHOD(DefaultDarkTime_Is2000)
        {
            LightSwitchConfig config;
            Assert::AreEqual(20 * 60, config.darkTime);
        }

        // Product code: LightSwitchTypes.h — LightSwitchConfig::latitude default
        // What: Verifies latitude defaults to "0.0" (null island sentinel)
        // Why: CoordinatesAreValid rejects (0,0) — forces user to configure location
        // Risk: Unconfigured users accidentally get sunset times for null island
        TEST_METHOD(DefaultLatitude_IsZero)
        {
            LightSwitchConfig config;
            Assert::AreEqual(std::wstring(L"0.0"), config.latitude);
        }

        // Product code: LightSwitchTypes.h — LightSwitchConfig::longitude default
        // What: Verifies longitude defaults to "0.0" (null island sentinel)
        // Why: Paired with latitude, ensures CoordinatesAreValid rejects defaults
        // Risk: Sunset calculations run for wrong location
        TEST_METHOD(DefaultLongitude_IsZero)
        {
            LightSwitchConfig config;
            Assert::AreEqual(std::wstring(L"0.0"), config.longitude);
        }

        // Product code: LightSwitchTypes.h — LightSwitchConfig::sunrise_offset default
        // What: Verifies sunrise offset defaults to 0 (no adjustment)
        // Why: Non-zero default would shift sunrise time unexpectedly
        // Risk: Sunrise-based schedule is off by the default offset minutes
        TEST_METHOD(DefaultSunriseOffset_IsZero)
        {
            LightSwitchConfig config;
            Assert::AreEqual(0, config.sunrise_offset);
        }

        // Product code: LightSwitchTypes.h — LightSwitchConfig::sunset_offset default
        // What: Verifies sunset offset defaults to 0 (no adjustment)
        // Why: Non-zero default would shift sunset time unexpectedly
        // Risk: Sunset-based schedule is off by the default offset minutes
        TEST_METHOD(DefaultSunsetOffset_IsZero)
        {
            LightSwitchConfig config;
            Assert::AreEqual(0, config.sunset_offset);
        }

        // Product code: LightSwitchTypes.h — LightSwitchConfig::changeSystem default
        // What: Verifies system theme changing is disabled by default
        // Why: Changing system theme without consent is disruptive
        // Risk: First-run silently changes user's system theme
        TEST_METHOD(DefaultChangeSystem_IsFalse)
        {
            LightSwitchConfig config;
            Assert::IsFalse(config.changeSystem);
        }

        // Product code: LightSwitchTypes.h — LightSwitchConfig::changeApps default
        // What: Verifies app theme changing is disabled by default
        // Why: Should require explicit opt-in to avoid surprising behavior
        // Risk: First-run silently changes user's app theme
        TEST_METHOD(DefaultChangeApps_IsFalse)
        {
            LightSwitchConfig config;
            Assert::IsFalse(config.changeApps);
        }

        // Product code: LightSwitchTypes.h — LightSwitchConfig profile defaults
        // What: Verifies display profile features are disabled with empty paths
        // Why: PowerDisplay integration must be opt-in; empty profiles = no action
        // Risk: Unintended display profile changes on first launch
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

        // Product code: ThemeScheduler.h — deg2rad(double)
        // What: Verifies 0 degrees converts to 0 radians
        // Why: Identity case for the conversion function
        // Risk: Broken conversion corrupts all sunrise/sunset calculations
        TEST_METHOD(Deg2Rad_Zero)
        {
            Assert::AreEqual(0.0, deg2rad(0.0), 0.0001);
        }

        // Product code: ThemeScheduler.h — deg2rad(double)
        // What: Verifies 90 degrees converts to π/2
        // Why: Quarter-circle is a common angle in trig calculations
        // Risk: Wrong conversion factor breaks all sun position math
        TEST_METHOD(Deg2Rad_90)
        {
            Assert::AreEqual(PI / 2.0, deg2rad(90.0), 0.0001);
        }

        // Product code: ThemeScheduler.h — deg2rad(double)
        // What: Verifies 180 degrees converts to π
        // Why: Half-circle validation — ensures PI constant is correct
        // Risk: Incorrect PI constant causes systematic sun position errors
        TEST_METHOD(Deg2Rad_180)
        {
            Assert::AreEqual(PI, deg2rad(180.0), 0.0001);
        }

        // Product code: ThemeScheduler.h — rad2deg(double)
        // What: Verifies 0 radians converts to 0 degrees
        // Why: Identity case for the inverse conversion
        // Risk: Broken inverse corrupts right ascension calculations
        TEST_METHOD(Rad2Deg_Zero)
        {
            Assert::AreEqual(0.0, rad2deg(0.0), 0.0001);
        }

        // Product code: ThemeScheduler.h — rad2deg(double)
        // What: Verifies π radians converts to 180 degrees
        // Why: Validates the inverse of deg2rad at a known reference point
        // Risk: Asymmetric conversion causes accumulated rounding errors
        TEST_METHOD(Rad2Deg_Pi)
        {
            Assert::AreEqual(180.0, rad2deg(PI), 0.0001);
        }

        // Product code: ThemeScheduler.h — deg2rad + rad2deg roundtrip
        // What: Verifies converting to radians and back preserves the original value
        // Why: Roundtrip consistency is required for correct astronomical calculations
        // Risk: Floating-point drift accumulates across multiple conversions
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

        // Product code: ThemeScheduler.cpp — CalculateSunriseSunset(lat, lon, y, m, d)
        // What: Verifies Seattle summer solstice produces valid times with 14+ hr daylight
        // Why: Summer solstice at 47°N is a well-known reference; validates the algorithm
        // Risk: Broken sun calc gives wrong schedule; users in Pacific NW get wrong theme
        TEST_METHOD(Seattle_JuneHasSunrise)
        {
            SunTimes times = CalculateSunriseSunset(47.6, -122.3, 2024, 6, 21);
            Assert::IsTrue(times.sunriseHour >= 0 && times.sunriseHour < 24,
                           L"Sunrise hour should be in [0,24)");
            Assert::IsTrue(times.sunsetHour >= 0 && times.sunsetHour < 24,
                           L"Sunset hour should be in [0,24)");
            Assert::IsTrue(times.sunriseMinute >= 0 && times.sunriseMinute < 60);
            Assert::IsTrue(times.sunsetMinute >= 0 && times.sunsetMinute < 60);

            int riseMinutes = times.sunriseHour * 60 + times.sunriseMinute;
            int setMinutes = times.sunsetHour * 60 + times.sunsetMinute;
            int daylight = setMinutes - riseMinutes;
            if (daylight < 0)
                daylight += 24 * 60;
            Assert::IsTrue(daylight >= 14 * 60,
                           L"Seattle June 21 should have at least 14 hours of daylight");
        }

        // Product code: ThemeScheduler.cpp — CalculateSunriseSunset(lat, lon, y, m, d)
        // What: Verifies equator in December produces ~12 hours of daylight
        // Why: Equator has nearly constant 12hr days year-round — validates baseline
        // Risk: Algorithm bias at equator causes wrong theme for tropical users
        TEST_METHOD(Equator_DecemberHasReasonableTimes)
        {
            SunTimes times = CalculateSunriseSunset(0.0, 0.0, 2024, 12, 21);
            Assert::IsTrue(times.sunriseHour >= 0 && times.sunriseHour < 24);
            Assert::IsTrue(times.sunsetHour >= 0 && times.sunsetHour < 24);

            int riseMinutes = times.sunriseHour * 60 + times.sunriseMinute;
            int setMinutes = times.sunsetHour * 60 + times.sunsetMinute;
            int daylight = setMinutes - riseMinutes;
            if (daylight < 0)
                daylight += 24 * 60;
            Assert::IsTrue(daylight >= 10 * 60 && daylight <= 14 * 60,
                           L"Equator should have 10-14 hours of daylight");
        }

        // Product code: ThemeScheduler.cpp — CalculateSunriseSunset(lat, lon, y, m, d)
        // What: Verifies New York equinox produces ~12 hours of daylight
        // Why: Equinox at any latitude should yield ~12 hr daylight — cross-validates
        // Risk: Algorithm error at mid-latitudes affects most users
        TEST_METHOD(SunriseBeforeSunset_NormalLatitude)
        {
            SunTimes times = CalculateSunriseSunset(40.0, -74.0, 2024, 3, 21);
            int riseMinutes = times.sunriseHour * 60 + times.sunriseMinute;
            int setMinutes = times.sunsetHour * 60 + times.sunsetMinute;
            int daylight = setMinutes - riseMinutes;
            if (daylight < 0)
                daylight += 24 * 60;
            Assert::IsTrue(daylight >= 11 * 60 && daylight <= 13 * 60,
                           L"Near equinox, daylight should be approximately 12 hours");
        }

        // Product code: ThemeScheduler.cpp — CalculateSunriseSunset(lat, lon, y, m, d)
        // What: Verifies Seattle winter solstice has 7-10 hours of daylight
        // Why: Shortest day at 47°N; ensures algorithm handles low sun angles
        // Risk: Winter users get sunrise/sunset times that are hours off
        TEST_METHOD(WinterSolstice_ShorterDays)
        {
            SunTimes times = CalculateSunriseSunset(47.6, -122.3, 2024, 12, 21);
            int riseMinutes = times.sunriseHour * 60 + times.sunriseMinute;
            int setMinutes = times.sunsetHour * 60 + times.sunsetMinute;
            int daylight = setMinutes - riseMinutes;
            if (daylight < 0)
                daylight += 24 * 60;
            Assert::IsTrue(daylight >= 7 * 60 && daylight <= 10 * 60,
                           L"Seattle Dec 21 should have 7-10 hours of daylight");
        }

        // Product code: ThemeScheduler.cpp — CalculateSunriseSunset(lat, lon, y, m, d)
        // What: Verifies Reykjavik (64°N) summer has 20+ hours of daylight
        // Why: Near-midnight-sun conditions stress the algorithm at high latitudes
        // Risk: High-latitude users get obviously wrong sunrise/sunset times
        TEST_METHOD(HighLatitude_LongSummerDay)
        {
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

        // Product code: ThemeScheduler.cpp — CalculateSunriseSunset() / calcTime lambda
        // What: Documents broken behavior at 70°N in December (polar night)
        // Why: When cosH > 1, calcTime returns -1 but toLocal() converts it to a
        //      timezone-adjusted garbage value instead of propagating an invalid sentinel.
        //      A correct implementation would flag sunrise/sunset as invalid.
        // Risk: Users in northern Norway/Iceland get random schedule times in winter
        TEST_METHOD(SunCalc_PolarNight_ReturnsInvalidFlag)
        {
            // 70°N, 25°E (Tromsø area), Dec 21 — sun never rises (polar night)
            // calcTime returns -1 for both sunrise and sunset.
            // Bug: toLocal(-1) applies timezone offset to -1, producing a nonsense hour.
            // e.g., UTC+1 → localTime = -1 + 1.0 = 0.0 → {hour=0, minute=0}
            //       UTC-8 → localTime = -1 + 8.0 = 7.0 → {hour=7, minute=0}
            // The returned values are meaningless — they look like valid times.
            SunTimes times = CalculateSunriseSunset(70.0, 25.0, 2024, 12, 21);

            // We cannot assert specific values because the garbage depends on the
            // machine's timezone. We just verify no crash and document the bug.
            // TODO: Fix CalculateSunriseSunset to return a proper invalid flag
            // (e.g., sunriseHour = -1) when the sun never rises/sets.
            Logger::WriteMessage(L"PolarNight: sunrise and sunset values are garbage "
                                 L"due to -1 sentinel being timezone-converted.");
            (void)times; // Suppress unused warning
        }

        // Product code: ThemeScheduler.cpp — CalculateSunriseSunset() / calcTime lambda
        // What: Documents broken behavior at 70°N in June (midnight sun)
        // Why: When cosH < -1 (sun never sets), calcTime returns -1 for sunset but
        //      sunrise may be valid. toLocal(-1) again produces garbage for sunset.
        // Risk: Midnight sun users get a fake sunset time, causing unwanted dark mode
        TEST_METHOD(SunCalc_MidnightSun_ReturnsInvalidFlag)
        {
            // 70°N, 25°E, June 21 — continuous daylight (midnight sun)
            // Sunrise may compute normally, but sunset's cosH < -1 → returns -1
            // toLocal(-1) converts to a timezone-dependent garbage hour
            SunTimes times = CalculateSunriseSunset(70.0, 25.0, 2024, 6, 21);

            // Document: at least one of the returned times is garbage.
            // A correct implementation would indicate "no sunset" so the scheduler
            // knows to keep light mode active for 24 hours.
            Logger::WriteMessage(L"MidnightSun: sunset value is garbage "
                                 L"due to -1 sentinel being timezone-converted.");
            (void)times;
        }
    };

    // ========================================================================
    // SettingId enum completeness
    // ========================================================================
    TEST_CLASS(SettingIdTests)
    {
    public:

        // Product code: SettingsConstants.h — SettingId enum
        // What: Verifies ScheduleMode is the first enum value (0)
        // Why: Observer notification uses SettingId; wrong ordinal = wrong dispatch
        // Risk: Settings changes notify the wrong observer, causing stale state
        TEST_METHOD(SettingId_ScheduleMode_IsZero)
        {
            Assert::AreEqual(0, static_cast<int>(SettingId::ScheduleMode));
        }

        // Product code: SettingsConstants.h — SettingId enum
        // What: Verifies all SettingId values are unique (no accidental duplicates)
        // Why: Duplicate IDs would cause one setting change to shadow another
        // Risk: Changing latitude also triggers longitude observer, or vice versa
        TEST_METHOD(SettingId_AllValuesDistinct)
        {
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

    // ========================================================================
    // CoordinatesAreValid — pure function from LightSwitchUtils.h
    // Guards the SunsetToSunrise mode from using garbage coordinates
    // ========================================================================
    TEST_CLASS(CoordinatesAreValidTests)
    {
    public:

        // Product code: LightSwitchUtils.h — CoordinatesAreValid(lat, lon)
        // What: Validates that Seattle coordinates (47.6, -122.3) are accepted
        // Why: A major US city must pass; rejection means SunsetToSunrise is broken
        // Risk: SunsetToSunrise mode silently falls back to fixed hours for valid locations
        TEST_METHOD(CoordinatesAreValid_ValidSeattle)
        {
            Assert::IsTrue(CoordinatesAreValid(L"47.6062", L"-122.3321"));
        }

        // Product code: LightSwitchUtils.h — CoordinatesAreValid(lat, lon)
        // What: Verifies (0, 0) is rejected as a "not configured" sentinel
        // Why: Default config uses "0.0"/"0.0" — must not trigger sun calculations
        // Risk: Unconfigured users get sunset times for Null Island in the Gulf of Guinea
        TEST_METHOD(CoordinatesAreValid_ZeroZero_RejectedAsSentinel)
        {
            Assert::IsFalse(CoordinatesAreValid(L"0.0", L"0.0"));
            Assert::IsFalse(CoordinatesAreValid(L"0", L"0"));
        }

        // Product code: LightSwitchUtils.h — CoordinatesAreValid(lat, lon)
        // What: Verifies latitude > 90 is rejected
        // Why: Valid latitude range is [-90, 90]; values outside cause trig errors
        // Risk: Invalid lat produces NaN in sun calc, causing undefined schedule
        TEST_METHOD(CoordinatesAreValid_OutOfRangeLat)
        {
            Assert::IsFalse(CoordinatesAreValid(L"91.0", L"0.1"));
            Assert::IsFalse(CoordinatesAreValid(L"-91.0", L"0.1"));
        }

        // Product code: LightSwitchUtils.h — CoordinatesAreValid(lat, lon)
        // What: Verifies longitude > 180 is rejected
        // Why: Valid longitude range is [-180, 180]; values outside are meaningless
        // Risk: Invalid lon produces wrong timezone offset in sun calculations
        TEST_METHOD(CoordinatesAreValid_OutOfRangeLon)
        {
            Assert::IsFalse(CoordinatesAreValid(L"0.1", L"181.0"));
            Assert::IsFalse(CoordinatesAreValid(L"0.1", L"-181.0"));
        }

        // Product code: LightSwitchUtils.h — CoordinatesAreValid(lat, lon)
        // What: Verifies southern hemisphere coordinates are accepted
        // Why: Negative lat/lon are valid; must not be rejected as "out of range"
        // Risk: All users south of the equator or west of Greenwich are broken
        TEST_METHOD(CoordinatesAreValid_NegativeValid)
        {
            // Sydney, Australia
            Assert::IsTrue(CoordinatesAreValid(L"-33.8688", L"151.2093"));
            // São Paulo, Brazil
            Assert::IsTrue(CoordinatesAreValid(L"-23.5505", L"-46.6333"));
        }

        // Product code: LightSwitchUtils.h — CoordinatesAreValid(lat, lon)
        // What: Verifies extreme but valid polar coordinates are accepted
        // Why: Polar regions (e.g., research stations) have valid coords near ±90
        // Risk: Edge coordinates rejected, blocking polar users from SunsetToSunrise
        TEST_METHOD(CoordinatesAreValid_ExtremeLatitude)
        {
            // Near North Pole (Svalbard research station)
            Assert::IsTrue(CoordinatesAreValid(L"78.2", L"15.6"));
            // Boundary values
            Assert::IsTrue(CoordinatesAreValid(L"90.0", L"0.1"));
            Assert::IsTrue(CoordinatesAreValid(L"-90.0", L"0.1"));
            Assert::IsTrue(CoordinatesAreValid(L"0.1", L"180.0"));
            Assert::IsTrue(CoordinatesAreValid(L"0.1", L"-180.0"));
        }
    };
}
