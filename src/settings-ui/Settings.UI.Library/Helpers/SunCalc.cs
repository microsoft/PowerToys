// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.PowerToys.Settings.UI.Library.Helpers
{
    public static class SunCalc
    {
        public static SunTimes CalculateSunriseSunset(double latitude, double longitude, int year, int month, int day)
        {
            double zenith = 90.833; // official sunrise/sunset

            int n1 = (int)Math.Floor(275.0 * month / 9.0);
            int n2 = (int)Math.Floor((month + 9.0) / 12.0);
            int n3 = (int)Math.Floor(1.0 + Math.Floor((year - (4.0 * Math.Floor(year / 4.0)) + 2.0) / 3.0));
            int n = n1 - (n2 * n3) + day - 30;

            double? riseUT = CalcTime(isSunrise: true);
            double? setUT = CalcTime(isSunrise: false);

            var riseLocal = ToLocal(riseUT, year, month, day);
            var setLocal = ToLocal(setUT, year, month, day);

            var result = new SunTimes
            {
                HasSunrise = riseLocal.HasValue,
                HasSunset = setLocal.HasValue,
                SunriseHour = riseLocal?.Hour ?? -1,
                SunriseMinute = riseLocal?.Minute ?? -1,
                SunsetHour = setLocal?.Hour ?? -1,
                SunsetMinute = setLocal?.Minute ?? -1,
            };

            return result;

            // Local functions
            double? CalcTime(bool isSunrise)
            {
                double lngHour = longitude / 15.0;
                double t = isSunrise ? n + ((6 - lngHour) / 24.0) : n + ((18 - lngHour) / 24.0);

                double m1 = (0.9856 * t) - 3.289;
                double l = m1 + (1.916 * Math.Sin(Deg2Rad(m1))) + (0.020 * Math.Sin(2 * Deg2Rad(m1))) + 282.634;
                l = NormalizeDegrees(l);

                double rA = Rad2Deg(Math.Atan(0.91764 * Math.Tan(Deg2Rad(l))));
                rA = NormalizeDegrees(rA);

                double lquadrant = Math.Floor(l / 90.0) * 90.0;
                double rAquadrant = Math.Floor(rA / 90.0) * 90.0;
                rA = rA + (lquadrant - rAquadrant);
                rA /= 15.0;

                double sinDec = 0.39782 * Math.Sin(Deg2Rad(l));
                double cosDec = Math.Cos(Math.Asin(sinDec));

                double cosH = (Math.Cos(Deg2Rad(zenith)) - (sinDec * Math.Sin(Deg2Rad(latitude))))
                            / (cosDec * Math.Cos(Deg2Rad(latitude)));

                if (cosH > 1.0 || cosH < -1.0)
                {
                    // Sun never rises or never sets on this date at this location
                    return null;
                }

                double h = isSunrise ? 360.0 - Rad2Deg(Math.Acos(cosH)) : Rad2Deg(Math.Acos(cosH));
                h /= 15.0;

                double t1 = h + rA - (0.06571 * t) - 6.622;
                double uT = t1 - lngHour;
                uT = NormalizeHours(uT);

                return uT;
            }

            static (int Hour, int Minute)? ToLocal(double? ut, int y, int m, int d)
            {
                if (!ut.HasValue)
                {
                    return null;
                }

                // Convert fractional hours to hh:mm with proper rounding
                int hours = (int)Math.Floor(ut.Value);
                int minutes = (int)((ut.Value - hours) * 60.0);

                // Normalize minute overflow
                if (minutes == 60)
                {
                    minutes = 0;
                    hours = (hours + 1) % 24;
                }

                // Build a UTC DateTime on the given date
                var utc = new DateTime(y, m, d, hours, minutes, 0, DateTimeKind.Utc);

                // Convert to local time using system time zone rules for that date
                var local = TimeZoneInfo.ConvertTimeFromUtc(utc, TimeZoneInfo.Local);

                return (local.Hour, local.Minute);
            }

            static double Deg2Rad(double deg) => deg * Math.PI / 180.0;
            static double Rad2Deg(double rad) => rad * 180.0 / Math.PI;

            static double NormalizeDegrees(double angle)
            {
                angle %= 360.0;
                if (angle < 0)
                {
                    angle += 360.0;
                }

                return angle;
            }

            static double NormalizeHours(double hours)
            {
                hours %= 24.0;
                if (hours < 0)
                {
                    hours += 24.0;
                }

                return hours;
            }
        }
    }
}
