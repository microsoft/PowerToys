// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.PowerToys.Run.Plugin.TimeZone.Classes;
using Microsoft.PowerToys.Run.Plugin.TimeZone.Properties;
using Mono.Collections.Generic;
using Wox.Plugin;

namespace Microsoft.PowerToys.Run.Plugin.TimeZone.Helper
{
    /// <summary>
    /// Helper class to easier work with results
    /// </summary>
    internal static class ResultHelper
    {
        /// <summary>
        /// Return a list of <see cref="Result"/>s based on the given <see cref="Query"/>.
        /// </summary>
        /// <param name="search">The <see cref="Query"/> to filter the <see cref="Results"/>.</param>
        /// <returns>A list with <see cref="Result"/>s.</returns>
        internal static IEnumerable<Result> GetResults(IEnumerable<OneTimeZone> timeZones, string search, string iconPath)
        {
            var results = new Collection<Result>();
            var utcNow = DateTime.UtcNow;

            foreach (var timeZone in timeZones)
            {
                if (TimeZoneInfoMatchQuery(timeZone, search))
                {
                    var result = GetResult(timeZone, utcNow, search, iconPath);
                    results.Add(result);
                }
            }

            var orderResults = results.OrderBy(result => result.Title);
            return orderResults;
        }

        /// <summary>
        /// Check if the given <see cref="TimeZoneInfoExtended"/> contains a value that match the given <see cref="Query"/> .
        /// </summary>
        /// <param name="timeZone">The <see cref="TimeZoneInfoExtended"/> to check.</param>
        /// <param name="search">The <see cref="Query"/> that should match.</param>
        /// <returns><see langword="true"/> if it's match, otherwise <see langword="false"/>.</returns>
        private static bool TimeZoneInfoMatchQuery(OneTimeZone timeZone, string search)
        {
            if (timeZone.Offset.Contains(search, StringComparison.InvariantCultureIgnoreCase))
            {
                return true;
            }

            if (timeZone.Name.Contains(search, StringComparison.CurrentCultureIgnoreCase))
            {
                return true;
            }

            if (timeZone.Shortcut != null
            && timeZone.Shortcut.Contains(search, StringComparison.InvariantCultureIgnoreCase))
            {
                return true;
            }

            if (timeZone.Countries != null
            && timeZone.Countries.Any(x => x.Contains(search, StringComparison.CurrentCultureIgnoreCase)))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Return a <see cref="Result"/> based on the given <see cref="TimeZoneInfoExtended"/>.
        /// </summary>
        /// <param name="timeZone">The <see cref="TimeZoneInfoExtended"/> that contain the information for the <see cref="Result"/>.</param>
        /// <param name="utcNow">The current time in UTC for the <see cref="Result"/>.</param>
        /// <returns>A <see cref="Result"/>.</returns>
        private static Result GetResult(OneTimeZone timeZone, DateTime utcNow, string search, string iconPath)
        {
            // TODO: revisit time zone names, maybe a list of time zone names
            // TODO: add standard and DST time zone names
            // TODO: add shortcuts
            var title = GetTitle(timeZone, utcNow);

            var result = new Result
            {
                ContextData = timeZone,
                IcoPath = iconPath,
                SubTitle = GetCountries(timeZone, search, maxLength: 100),
                Title = title,
                ToolTipData = new ToolTipData(title, GetToolTip(timeZone)),
            };

            return result;
        }

        private static DateTime GetTimeInTimeZone(OneTimeZone timeZone, DateTime utcNow)
        {
            DateTime result;

            var (hours, minutes) = GetHoursAndMinutes(timeZone);

            if (timeZone.Offset.StartsWith("-", StringComparison.InvariantCultureIgnoreCase))
            {
                result = utcNow.AddHours(-hours).AddMinutes(-minutes);
            }
            else
            {
                result = utcNow.AddHours(hours).AddMinutes(minutes);
            }

            if (timeZone.DaylightSavingTime)
            {
                result = result.AddHours(1);
            }

            return result;
        }

        private static string GetTitle(OneTimeZone timeZone, DateTime utcNow)
        {
            string timeZoneName;

            if (string.IsNullOrEmpty(timeZone.Name))
            {
                timeZoneName = $"UTC{GetFullOffset(timeZone)}";

                if (timeZone.DaylightSavingTime)
                {
                    timeZoneName = $"{timeZoneName} - {Resources.DaylightSavingTime}";
                }
            }
            else
            {
                timeZoneName = timeZone.Name;
            }

            var timeInZoneTime = GetTimeInTimeZone(timeZone, utcNow);

            return $"{timeInZoneTime:HH:mm:ss} - {timeZoneName}";
        }

        private static string GetToolTip(OneTimeZone timeZone)
        {
            var useDst = timeZone.DaylightSavingTime ? Resources.Yes : Resources.No;

            var countries = GetCountries(timeZone, search: string.Empty, maxLength: int.MaxValue);

            var result = $"{Environment.NewLine}{Resources.Name}: {timeZone.Name}"
                       + $"{Environment.NewLine}{Resources.Offset}: {GetFullOffset(timeZone)}"
                       + $"{Environment.NewLine}{Resources.DaylightSavingTime}:{useDst}"
                       + $"{Environment.NewLine}{Resources.Countries}:{countries}";

            return result;
        }

        private static (byte hours, byte minutes) GetHoursAndMinutes(OneTimeZone timeZone)
        {
            string offset;

            if (timeZone.Offset.StartsWith("-", StringComparison.InvariantCultureIgnoreCase))
            {
                offset = timeZone.Offset.Substring(1);
            }
            else
            {
                offset = timeZone.Offset;
            }

            var offsetSplit = offset.Split(':');

            if (byte.TryParse(offsetSplit.FirstOrDefault(), out var hours) && byte.TryParse(offsetSplit.LastOrDefault(), out var minutes))
            {
                return (hours, minutes);
            }

            return (0, 0);
        }

        private static string GetCountries(OneTimeZone timeZone, string search, int maxLength)
        {
            IEnumerable<string> countries;

            // TODO: translate country names
            if (string.IsNullOrWhiteSpace(search))
            {
                countries = timeZone.Countries;
            }
            else
            {
                // INFO: I'm not sure if "CurrentCultureIgnoreCase" is correct here
                countries = timeZone.Countries.Where(x => x.Contains(search, StringComparison.CurrentCultureIgnoreCase));
                if (!countries.Any())
                {
                    countries = timeZone.Countries;
                }
            }

            var result = countries.Aggregate(string.Empty, (current, next)
                                    => current.Length == 0 ? next : $"{current}, {next}");

            // To many countries => reduce length, first pass
            if (result.Length > maxLength)
            {
                result = countries.Aggregate(string.Empty, (current, next)
                                    => current.Length == 0 ? $"{next[..3]}..." : $"{current}, {next[..3]}...");
            }

            // To many countries => reduce length, second pass
            if (result.Length > maxLength)
            {
                result = $"{result[..maxLength]}...";
            }

            return result;
        }

        private static string GetFullOffset(OneTimeZone timeZone)
        {
            var (hours, minutes) = GetHoursAndMinutes(timeZone);

            string result;

            if (timeZone.Offset.StartsWith('-'))
            {
                result = "-{hours}:{minutes:00}";
            }
            else
            {
                result = $"+{hours}:{minutes:00}";
            }

            return result;
        }
    }
}
