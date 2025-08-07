#include "pch.h"
#include "MainWindow.xaml.h"

#if __has_include("MainWindow.g.cpp")
#include "MainWindow.g.cpp"
#endif

#include "ThemeHelper.h"
#include <chrono>
#include <winrt/Windows.Devices.Geolocation.h>
#include <cmath>
#include <ctime>

using namespace winrt;
using namespace winrt::Windows::Devices::Geolocation;
using namespace Microsoft::UI::Xaml;
using namespace Windows::Foundation;

namespace winrt::PowerToys::DarkMode::implementation
{
    MainWindow::MainWindow()
    {
        InitializeComponent();

        // Set initial checkbox state based on current theme
        SystemCheckbox().IsChecked(GetCurrentSystemTheme());
        AppsCheckbox().IsChecked(GetCurrentAppsTheme());

        // Start the timer aligned with the next minute
        ScheduleNextTick();
    }

    void MainWindow::ApplyButton_Click(IInspectable const&, RoutedEventArgs const&)
    {
        lightTime = LightTimePicker().Time();
        darkTime = DarkTimePicker().Time();
    }

    void MainWindow::ScheduleNextTick()
    {
        SYSTEMTIME st;
        GetLocalTime(&st);

        // Calculate ms until the next minute
        int msUntilNextMinute = (60 - st.wSecond) * 1000 - st.wMilliseconds;
        if (msUntilNextMinute < 0)
            msUntilNextMinute = 0;

        // Create a new timer and store it so it stays alive
        m_timer = this->DispatcherQueue().CreateTimer();
        m_timer.Interval(std::chrono::milliseconds(msUntilNextMinute));

        m_timer.Tick([this](auto const&, auto const&) {
            OnTimerTick(nullptr, nullptr); // Fire at minute boundary
            ScheduleNextTick(); // Schedule again for next minute
        });

        m_timer.Start();
    }

    void MainWindow::OnTimerTick(IInspectable const&, IInspectable const&)
    {
        SYSTEMTIME st;
        GetLocalTime(&st);

        int nowMinutes = st.wHour * 60 + st.wMinute;

        auto lightMinutes = static_cast<int>((lightTime.count() / 10'000'000 / 60) % (24 * 60));
        auto darkMinutes = static_cast<int>((darkTime.count() / 10'000'000 / 60) % (24 * 60));

        if (nowMinutes == lightMinutes)
        {
            if (SystemCheckbox().IsChecked().GetBoolean())
                SetSystemTheme(true); // true = light
            if (AppsCheckbox().IsChecked().GetBoolean())
                SetAppsTheme(true);
        }

        if (nowMinutes == darkMinutes)
        {
            if (SystemCheckbox().IsChecked().GetBoolean())
                SetSystemTheme(false); // false = dark
            if (AppsCheckbox().IsChecked().GetBoolean())
                SetAppsTheme(false);
        }
    }

    void MainWindow::ForceLight_Click(IInspectable const&, RoutedEventArgs const&)
    {
        if (SystemCheckbox().IsChecked().GetBoolean())
            SetSystemTheme(true);
        if (AppsCheckbox().IsChecked().GetBoolean())
            SetAppsTheme(true);
    }

    void MainWindow::ForceDark_Click(IInspectable const&, RoutedEventArgs const&)
    {
        if (SystemCheckbox().IsChecked().GetBoolean())
            SetSystemTheme(false);
        if (AppsCheckbox().IsChecked().GetBoolean())
            SetAppsTheme(false);
    }

    void MainWindow::ModeRadio_Checked(IInspectable const&, RoutedEventArgs const&)
    {
        if (TimeModeRadio() && GeoPanel() && TimePickerPanel())
        {
            if (TimeModeRadio().IsChecked().GetBoolean())
            {
                TimePickerPanel().Visibility(Visibility::Visible);
                GeoPanel().Visibility(Visibility::Collapsed);
            }
            else
            {
                TimePickerPanel().Visibility(Visibility::Collapsed);
                GeoPanel().Visibility(Visibility::Visible);
            }
        }
    }

    void MainWindow::GetLocation_Click(IInspectable const&, RoutedEventArgs const&)
    {
        Geolocator geolocator;
        geolocator.DesiredAccuracy(PositionAccuracy::Default);

        geolocator.GetGeopositionAsync().Completed([this](auto const& async, auto const&) {
            try
            {
                auto geoposition = async.GetResults();
                m_latitude = geoposition.Coordinate().Point().Position().Latitude;
                m_longitude = geoposition.Coordinate().Point().Position().Longitude;

                DispatcherQueue().TryEnqueue([this]() {
                    LatitudeText().Text(to_hstring(m_latitude));
                    LongitudeText().Text(to_hstring(m_longitude));

                    UpdateSunriseSunset();
                });
            }
            catch (...)
            {
                DispatcherQueue().TryEnqueue([this]() {
                    LatitudeText().Text(L"Unavailable");
                    LongitudeText().Text(L"Unavailable");
                });
            }
        });
    }

    struct SunTimes
    {
        int sunriseHour;
        int sunriseMinute;
        int sunsetHour;
        int sunsetMinute;
    };

    // Helpers to convert degrees to radians
    constexpr double PI = 3.14159265358979323846;
    constexpr double deg2rad(double deg)
    {
        return deg * PI / 180.0;
    }
    constexpr double rad2deg(double rad)
    {
        return rad * 180.0 / PI;
    }

    // NOAA Solar Calculation
    SunTimes CalculateSunriseSunset(double latitude, double longitude, int year, int month, int day)
    {
        double zenith = 90.833; // Official sunrise/sunset
        int N1 = static_cast<int>(floor(275.0 * month / 9.0));
        int N2 = static_cast<int>(floor((static_cast<double>(month) + 9) / 12.0));
        int N3 = static_cast<int>(floor((1.0 + floor((year - 4.0 * floor(year / 4.0) + 2.0) / 3.0))));
        int N = N1 - (N2 * N3) + day - 30;

        auto calcTime = [&](bool sunrise) -> double {
            double lngHour = longitude / 15.0;
            double t = sunrise ? N + ((6 - lngHour) / 24) : N + ((18 - lngHour) / 24);

            double M = (0.9856 * t) - 3.289;
            double L = M + (1.916 * sin(deg2rad(M))) + (0.020 * sin(2 * deg2rad(M))) + 282.634;
            if (L < 0)
                L += 360;
            if (L > 360)
                L -= 360;

            double RA = rad2deg(atan(0.91764 * tan(deg2rad(L))));
            if (RA < 0)
                RA += 360;
            if (RA > 360)
                RA -= 360;

            double Lquadrant = floor(L / 90) * 90;
            double RAquadrant = floor(RA / 90) * 90;
            RA = RA + (Lquadrant - RAquadrant);
            RA /= 15;

            double sinDec = 0.39782 * sin(deg2rad(L));
            double cosDec = cos(asin(sinDec));

            double cosH = (cos(deg2rad(zenith)) - (sinDec * sin(deg2rad(latitude)))) / (cosDec * cos(deg2rad(latitude)));
            if (cosH > 1)
                return -1; // Sun never rises
            if (cosH < -1)
                return -1; // Sun never sets

            double H = sunrise ? 360 - rad2deg(acos(cosH)) : rad2deg(acos(cosH));
            H /= 15;

            double T = H + RA - (0.06571 * t) - 6.622;
            double UT = T - lngHour;
            while (UT < 0)
                UT += 24;
            while (UT >= 24)
                UT -= 24;

            return UT;
        };

        double riseUT = calcTime(true);
        double setUT = calcTime(false);

        auto toLocal = [](double UT) {
            // Get local time offset in hours
            TIME_ZONE_INFORMATION tz;
            DWORD state = GetTimeZoneInformation(&tz);

            double totalBias = tz.Bias;
            if (state == TIME_ZONE_ID_DAYLIGHT)
            {
                totalBias += tz.DaylightBias; // Apply daylight offset
            }
            else if (state == TIME_ZONE_ID_STANDARD)
            {
                totalBias += tz.StandardBias;
            }

            double biasHours = -(totalBias / 60.0);

            double localTime = UT + biasHours;
            while (localTime < 0)
                localTime += 24;
            while (localTime >= 24)
                localTime -= 24;

            int hour = static_cast<int>(localTime);
            int minute = static_cast<int>((localTime - hour) * 60);
            return std::pair<int, int>{ hour, minute };
        };

        auto [riseHour, riseMinute] = toLocal(riseUT);
        auto [setHour, setMinute] = toLocal(setUT);

        SunTimes result;
        result.sunriseHour = riseHour;
        result.sunriseMinute = riseMinute;
        result.sunsetHour = setHour;
        result.sunsetMinute = setMinute;

        return result;
    }

    void MainWindow::UpdateSunriseSunset()
    {
        SYSTEMTIME st;
        GetLocalTime(&st);

        auto sunTimes = CalculateSunriseSunset(m_latitude, m_longitude, st.wYear, st.wMonth, st.wDay);

        DispatcherQueue().TryEnqueue([this, sunTimes]() {
            SunriseText().Text(L"Sunrise: " + std::to_wstring(sunTimes.sunriseHour) + L":" + std::to_wstring(sunTimes.sunriseMinute));
            SunsetText().Text(L"Sunset: " + std::to_wstring(sunTimes.sunsetHour) + L":" + std::to_wstring(sunTimes.sunsetMinute));

            // Save times as TimeSpans for theme switching
            lightTime = std::chrono::hours(sunTimes.sunriseHour) + std::chrono::minutes(sunTimes.sunriseMinute);
            darkTime = std::chrono::hours(sunTimes.sunsetHour) + std::chrono::minutes(sunTimes.sunsetMinute);
        });
    }
}
