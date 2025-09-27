#include "ThemeScheduler.h"
#include <utility>

SunTimes CalculateSunriseSunset(double latitude, double longitude, int year, int month, int day)
{
    double zenith = 90.833;
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
        if (cosH > 1 || cosH < -1)
            return -1;

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
        TIME_ZONE_INFORMATION tz;
        DWORD state = GetTimeZoneInformation(&tz);
        double totalBias = tz.Bias;

        if (state == TIME_ZONE_ID_DAYLIGHT)
            totalBias += tz.DaylightBias;
        else if (state == TIME_ZONE_ID_STANDARD)
            totalBias += tz.StandardBias;

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
