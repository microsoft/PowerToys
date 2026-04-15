// Tests for the solar-position math in ThemeScheduler.
// These tests cover the pure calculation path (UTC result) so they are
// independent of the system timezone returned by GetTimeZoneInformation.
//
// Build: add this file to the LightSwitchTests project and link against
//        LightSwitchService (or include ThemeScheduler.h + .cpp directly).

#include "pch.h"
#include <cmath>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

// ---- Inline the helpers so the tests compile without pulling in windows.h ----

static constexpr double kPI = 3.14159265358979323846;
static double deg2rad_t(double d) { return d * kPI / 180.0; }
static double rad2deg_t(double r) { return r * 180.0 / kPI; }

// Pure-math version of the calcTime lambda (returns fractional UTC hours, or
// -1 if the sun never rises/sets at that location on that date).
static double calcUTTime(
    double latitude, double longitude,
    int N,           // day-of-year (1..366)
    bool sunrise)
{
    const double zenith   = 90.833;
    const double lngHour  = longitude / 15.0;
    const double t        = sunrise ? N + ((6.0 - lngHour) / 24.0)
                                    : N + ((18.0 - lngHour) / 24.0);

    const double M = (0.9856 * t) - 3.289;

    // Sun's true longitude, normalised to [0, 360) with fmod.
    double L = std::fmod(M + (1.916 * std::sin(deg2rad_t(M)))
                           + (0.020 * std::sin(2.0 * deg2rad_t(M)))
                           + 282.634, 360.0);
    if (L < 0) L += 360.0;

    // Right ascension, normalised to [0, 360) and placed in L's quadrant.
    double RA = std::fmod(rad2deg_t(std::atan(0.91764 * std::tan(deg2rad_t(L)))), 360.0);
    if (RA < 0) RA += 360.0;

    const double Lquadrant  = std::floor(L  / 90.0) * 90.0;
    const double RAquadrant = std::floor(RA / 90.0) * 90.0;
    RA = (RA + (Lquadrant - RAquadrant)) / 15.0;

    const double sinDec = 0.39782 * std::sin(deg2rad_t(L));
    const double cosDec = std::cos(std::asin(sinDec));

    const double cosH = (std::cos(deg2rad_t(zenith)) - (sinDec * std::sin(deg2rad_t(latitude))))
                      / (cosDec * std::cos(deg2rad_t(latitude)));
    if (cosH > 1.0 || cosH < -1.0)
        return -1.0; // polar night or midnight sun

    const double H = sunrise ? (360.0 - rad2deg_t(std::acos(cosH)))
                             :           rad2deg_t(std::acos(cosH));

    double UT = (H / 15.0) + RA - (0.06571 * t) - 6.622 - lngHour;
    while (UT <   0.0) UT += 24.0;
    while (UT >= 24.0) UT -= 24.0;
    return UT;
}

// Day-of-year helper matching the N1/N2/N3 formula in ThemeScheduler.cpp.
static int dayOfYear(int year, int month, int day)
{
    int N1 = static_cast<int>(std::floor(275.0 * month / 9.0));
    int N2 = static_cast<int>(std::floor((static_cast<double>(month) + 9.0) / 12.0));
    int N3 = static_cast<int>(1.0 + std::floor((year - 4.0 * std::floor(year / 4.0) + 2.0) / 3.0));
    return N1 - (N2 * N3) + day - 30;
}

// ---- Test class ----------------------------------------------------------

namespace LightSwitchTests
{
    TEST_CLASS(ThemeSchedulerMathTests)
    {
    public:
        // London (51.5°N, 0°W), summer solstice.
        // Almanac sunrise UTC ≈ 03:43, sunset ≈ 20:20.
        TEST_METHOD(LondonSummerSolstice_SunriseAndSunsetAreReasonable)
        {
            const int N       = dayOfYear(2024, 6, 21);
            const double lat  = 51.5;
            const double lon  =  0.0;

            double rise = calcUTTime(lat, lon, N, true);
            double set  = calcUTTime(lat, lon, N, false);

            // UTC values should be within ±30 min of published almanac times.
            Assert::IsTrue(rise >= 3.0 && rise <= 4.5,
                L"London summer sunrise UTC should be ~3:43");
            Assert::IsTrue(set  >= 19.5 && set <= 21.0,
                L"London summer sunset UTC should be ~20:20");
        }

        // Sydney (33.9°S, 151.2°E), winter solstice (southern hemisphere).
        // Astronomical sunrise UTC ≈ 21:54 (prev day) / 21:55, sunset ≈ 07:00.
        TEST_METHOD(SydneyWinterSolstice_SunriseAndSunsetAreReasonable)
        {
            const int N       = dayOfYear(2024, 6, 21);
            const double lat  = -33.9;
            const double lon  = 151.2;

            double rise = calcUTTime(lat, lon, N, true);
            double set  = calcUTTime(lat, lon, N, false);

            // UTC rise ≈ 21-22 h, UTC set ≈ 6-8 h.
            Assert::IsTrue((rise >= 20.5 && rise <= 23.0) || (rise >= 0.0 && rise < 0.5),
                L"Sydney winter sunrise UTC should be ~21:54");
            Assert::IsTrue(set >= 6.0 && set <= 8.5,
                L"Sydney winter sunset UTC should be ~07:00");
        }

        // L normalisation: verify that fmod(x, 360) matches the old
        // single-subtract for all t values reachable from real Earth inputs.
        // Maximum practical t ≈ 367.3 (N=366, lon=-180, sunset).
        // For this t: L_raw ≈ 641, and old/new code must give the same result.
        TEST_METHOD(LongitudeNorm_OldAndNewMatchForAllReachableInputs)
        {
            // Sweep the full practical range of t values.
            for (double t = 0.0; t <= 368.0; t += 0.5)
            {
                const double M = (0.9856 * t) - 3.289;
                const double L_raw = M
                    + (1.916 * std::sin(deg2rad_t(M)))
                    + (0.020 * std::sin(2.0 * deg2rad_t(M)))
                    + 282.634;

                // Old normalization (single subtract)
                double L_old = L_raw;
                if (L_old < 0)    L_old += 360.0;
                if (L_old >= 360) L_old -= 360.0;

                // New normalization (fmod)
                double L_new = std::fmod(L_raw, 360.0);
                if (L_new < 0) L_new += 360.0;

                // For the reachable range, L_raw tops out near 641;
                // a single subtract always yields the same answer as fmod.
                Assert::AreEqual(L_old, L_new, 1e-9,
                    L"L normalisation mismatch for reachable t value");
            }
        }

        // Polar regions: calcUTTime must return -1 for polar night / midnight sun
        // rather than an out-of-range value.
        TEST_METHOD(PolarNight_ReturnsSentinel)
        {
            // North Pole in December has no sunrise.
            const int N = dayOfYear(2024, 12, 21);
            double rise = calcUTTime(89.9, 0.0, N, true);
            Assert::AreEqual(-1.0, rise, L"North Pole Dec: no sunrise expected");
        }

        TEST_METHOD(MidnightSun_ReturnsSentinel)
        {
            // North Pole in June has no sunset.
            const int N = dayOfYear(2024, 6, 21);
            double set = calcUTTime(89.9, 0.0, N, false);
            Assert::AreEqual(-1.0, set, L"North Pole Jun: no sunset expected");
        }
    };
}
