// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.PowerToys.Run.Plugin.TimeZone.Classes;
using Microsoft.PowerToys.Run.Plugin.TimeZone.Extensions;
using Microsoft.PowerToys.Run.Plugin.TimeZone.Properties;
using Mono.Collections.Generic;
using Wox.Plugin;

// TODO: revisit time zone names
// TODO: revisit standard time names
// TODO: revisit DST time names
// TODO: add more time zone shortcuts
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
        /// <param name="timeZones">A list with all possible time zones.</param>
        /// <param name="options">Additional options to limit the results.</param>
        /// <param name="query">The <see cref="Query"/> to filter the <see cref="Results"/>.</param>
        /// <param name="iconPath">The path to the icon that is used for each result.</param>
        /// <returns>A list with <see cref="Result"/>s.</returns>
        internal static IEnumerable<Result> GetResults(in IEnumerable<OneTimeZone> timeZones, in TimeZoneSettings options, in Query query, in string iconPath)
        {
            var results = new List<Result>();
            var utcNow = DateTime.UtcNow;

            foreach (var timeZone in timeZones)
            {
                if (MatchShortcuts(timeZone, query)
                || MatchNames(timeZone, query)
                || MatchStandardNames(timeZone, query)
                || MatchDaylightNames(timeZone, query)
                || MatchStandardCountries(timeZone, query)
                || MatchDaylightCountries(timeZone, query)
                || MatchOffset(timeZone, query))
                {
                    results.AddRange(GetResults(timeZone, options, query, iconPath, utcNow));
                }
            }

            var orderResults = results.OrderBy(result => result.Title);
            return orderResults;
        }

        /// <summary>
        /// Return a list with <see cref="Result"/> based on the given time zone.
        /// </summary>
        /// <param name="timeZone">The time zone that contain the information for the <see cref="Result"/>.</param>
        /// <param name="options">Additional options to limit the results.</param>
        /// <param name="query">The <see cref="Query"/> that should match.</param>
        /// <param name="iconPath">The path to the icon that is used for each result.</param>
        /// <param name="utcNow">The current time in UTC for the <see cref="Result"/>.</param>
        /// <returns>A list with <see cref="Result"/>.</returns>
        private static IEnumerable<Result> GetResults(in OneTimeZone timeZone, in TimeZoneSettings options, in Query query, in string iconPath, in DateTime utcNow)
        {
            var results = new Collection<Result>();

            var standardTitle = GetTitle(timeZone, options, utcNow, false);
            var daylightTitle = GetTitle(timeZone, options, utcNow, true);

            if (standardTitle == daylightTitle)
            {
                results.Add(new Result
                {
                    ContextData = GetTimeInTimeZone(timeZone, utcNow, false),
                    IcoPath = iconPath,
                    Title = standardTitle,
                    SubTitle = GetAllCountries(timeZone, query, maxLength: 100),
                    ToolTipData = new ToolTipData(standardTitle, GetAllToolTip(timeZone, options)),
                });

                return results;
            }

            if (MatchStandardCountries(timeZone, query) || MatchStandardNames(timeZone, query) || MatchNames(timeZone, query) || MatchOffset(timeZone, query))
            {
                results.Add(new Result
                {
                    ContextData = GetTimeInTimeZone(timeZone, utcNow, false),
                    IcoPath = iconPath,
                    SubTitle = GetStandardCountries(timeZone, query, maxLength: 100),
                    Title = standardTitle,
                    ToolTipData = new ToolTipData(standardTitle, GetStandardToolTip(timeZone, options)),
                });
            }

            if (MatchDaylightCountries(timeZone, query) || MatchDaylightNames(timeZone, query) || MatchNames(timeZone, query) || MatchOffset(timeZone, query))
            {
                results.Add(new Result
                {
                    ContextData = GetTimeInTimeZone(timeZone, utcNow, true),
                    IcoPath = iconPath,
                    SubTitle = GetDaylightCountries(timeZone, query, maxLength: 100),
                    Title = daylightTitle,
                    ToolTipData = new ToolTipData(daylightTitle, GetDaylightToolTip(timeZone, options)),
                });
            }

            return results;
        }

        /// <summary>
        /// Return the current local time of the given time zone.
        /// </summary>
        /// <param name="timeZone">The time zone that contain all information.</param>
        /// <param name="utcNow">The current time in UTC.</param>
        /// <param name="daylightSavingTime">indicate that the result is for a time zone that use a daylight saving time.</param>
        /// <returns>The current local time in a time zone.</returns>
        private static DateTime GetTimeInTimeZone(in OneTimeZone timeZone, in DateTime utcNow, in bool daylightSavingTime)
        {
            foreach (var timeZoneInfo in TimeZoneInfo.GetSystemTimeZones())
            {
                if (timeZoneInfo.BaseUtcOffset == timeZone.OffsetAsTimeSpan
                && timeZoneInfo.SupportsDaylightSavingTime == daylightSavingTime)
                {
                    return TimeZoneInfo.ConvertTime(utcNow, timeZoneInfo);
                }
            }

            // Fall-back
            var result = utcNow + timeZone.OffsetAsTimeSpan;
            return result;
        }

        /// <summary>
        /// Return the title for the given time zone.
        /// </summary>
        /// <param name="timeZone">The time zone that contain all information.</param>
        /// <param name="options">Additional options to limit the results.</param>
        /// <param name="utcNow">The current time in UTC.</param>
        /// <param name="daylightSavingTime">indicate that the result is for a time zone that use a daylight saving time.</param>
        /// <returns>A title for a time zone.</returns>
        private static string GetTitle(in OneTimeZone timeZone, in TimeZoneSettings options, in DateTime utcNow, in bool daylightSavingTime)
        {
            var timeInZoneTime = GetTimeInTimeZone(timeZone, utcNow, daylightSavingTime);
            var timeZoneNames = GetNames(timeZone, options, maxLength: 50);

            return $"{timeInZoneTime:HH:mm:ss} - {timeZoneNames}";
        }

        /// <summary>
        /// Return a tool-tip for the given time zone with countries that use the standard time.
        /// </summary>
        /// <param name="timeZone">The time zone that contain all information.</param>
        /// <param name="options">Additional options to limit the results.</param>
        /// <returns>A tool-tip with countries that use the standard time.</returns>
        private static string GetStandardToolTip(in OneTimeZone timeZone, in TimeZoneSettings options)
        {
            var countries = GetStandardCountries(timeZone, null, maxLength: int.MaxValue);
            var names = GetNames(timeZone, options, maxLength: int.MaxValue);

            var stringBuilder = new StringBuilder();

            stringBuilder.Append(Resources.Offset).Append(':').Append(' ').AppendLine(timeZone.Offset);
            stringBuilder.Append(Resources.UseDst).Append(':').Append(' ').AppendLine(Resources.No);
            stringBuilder.AppendLine(string.Empty);
            stringBuilder.Append(Resources.Names).Append(':').Append(' ').AppendLine(names);
            stringBuilder.AppendLine(string.Empty);
            stringBuilder.Append(Resources.Countries).Append(':').Append(' ').AppendLine(countries);

            return stringBuilder.ToString();
        }

        /// <summary>
        /// Return a tool-tip for the given time zone with countries that use the daylight saving time.
        /// </summary>
        /// <param name="timeZone">The time zone that contain all information.</param>
        /// <param name="options">Additional options to limit the type of the names.</param>
        /// <returns>A tool-tip with countries that use the daylight saving time.</returns>
        private static string GetDaylightToolTip(in OneTimeZone timeZone, in TimeZoneSettings options)
        {
            var dstCountries = GetDaylightCountries(timeZone, null, maxLength: int.MaxValue);
            var names = GetNames(timeZone, options, maxLength: int.MaxValue);

            var stringBuilder = new StringBuilder();

            stringBuilder.Append(Resources.Offset).Append(':').Append(' ').AppendLine(timeZone.Offset);
            stringBuilder.Append(Resources.UseDst).Append(':').Append(' ').AppendLine(Resources.Yes);
            stringBuilder.AppendLine(string.Empty);
            stringBuilder.Append(Resources.Names).Append(':').Append(' ').AppendLine(names);
            stringBuilder.AppendLine(string.Empty);
            stringBuilder.Append(Resources.CountriesWithDst).Append(':').Append(' ').AppendLine(dstCountries);

            return stringBuilder.ToString();
        }

        /// <summary>
        /// Return a tool-tip for the given time zone with countries.
        /// </summary>
        /// <param name="timeZone">The time zone that contain all information.</param>
        /// <param name="options">Additional options to limit the type of the names.</param>
        /// <returns>A tool-tip with countries.</returns>
        private static string GetAllToolTip(in OneTimeZone timeZone, in TimeZoneSettings options)
        {
            var countries = GetStandardCountries(timeZone, null, maxLength: int.MaxValue);
            var dstCountries = GetDaylightCountries(timeZone, null, maxLength: int.MaxValue);
            var names = GetNames(timeZone, options, maxLength: int.MaxValue);

            var stringBuilder = new StringBuilder();

            stringBuilder.Append(Resources.Offset).Append(':').Append(' ').AppendLine(timeZone.Offset);
            stringBuilder.Append(Resources.UseDst).Append(':').Append(' ').AppendLine(Resources.Yes);
            stringBuilder.AppendLine(string.Empty);
            stringBuilder.Append(Resources.Names).Append(':').Append(' ').AppendLine(names);
            stringBuilder.AppendLine(string.Empty);
            stringBuilder.Append(Resources.Countries).Append(':').Append(' ').AppendLine(countries);
            stringBuilder.AppendLine(string.Empty);
            stringBuilder.Append(Resources.CountriesWithDst).Append(':').Append(' ').AppendLine(dstCountries);

            return stringBuilder.ToString();
        }

        /// <summary>
        /// Return a <see cref="string"/> that contain all know names of the given time zone.
        /// </summary>
        /// <param name="timeZone">The time zone that contain a hand of names.</param>
        /// <param name="options">Additional options to limit the type of the names.</param>
        /// <param name="maxLength">The maximum length of the result <see cref="string"/>.</param>
        /// <returns>A <see cref="string"/> that contain names of a given time zone.</returns>
        private static string GetNames(in OneTimeZone timeZone, in TimeZoneSettings options, in int maxLength)
        {
            var names = new List<string>();

            if (!string.IsNullOrWhiteSpace(timeZone.Name) && options.ShowTimeZoneNames)
            {
                names.Add(timeZone.Name);
            }

            if (!string.IsNullOrWhiteSpace(timeZone.MilitaryName) && options.ShowMilitaryTimeZoneNames)
            {
                names.Add(timeZone.MilitaryName);
            }

            if (timeZone.TimeNamesStandard != null && options.ShowTimeZoneNames)
            {
                names.AddRange(timeZone.TimeNamesStandard);
            }

            if (timeZone.TimeNamesDaylight != null && options.ShowTimeZoneNames)
            {
                names.AddRange(timeZone.TimeNamesDaylight);
            }

            if (names.Any())
            {
                var stringBuilder = new StringBuilder();
                var lastEntry = names.LastOrDefault();

                foreach (var name in names)
                {
                    stringBuilder.Append(name);

                    if (name != lastEntry)
                    {
                        stringBuilder.Append(',');
                        stringBuilder.Append(' ');
                    }
                }

                // To many names (first pass) => use shortcuts
                if (stringBuilder.Length > maxLength)
                {
                    stringBuilder.Replace(Resources.TimeZone, Resources.TimeZoneShortcut);
                    stringBuilder.Replace(Resources.StandardTime, Resources.TimeZoneShortcut);
                    stringBuilder.Replace(Resources.DaylightTime, Resources.DaylightTimeShortcut);
                    stringBuilder.Replace(Resources.Time, Resources.TimeShortcut);
                }

                // To many names (second pass) => cut name length
                if (stringBuilder.Length > maxLength)
                {
                    foreach (var country in names)
                    {
                        stringBuilder.SaveAppend(country, maxLength: 5);

                        if (country != lastEntry)
                        {
                            stringBuilder.Append(',');
                            stringBuilder.Append(' ');
                        }
                    }
                }

                stringBuilder.CutTooLong(maxLength);

                return stringBuilder.ToString();
            }
            else
            {
                // fall-back
                var totalMinutes = timeZone.OffsetAsTimeSpan.TotalMinutes;
                if (totalMinutes < 0)
                {
                    return $"UTC{timeZone.Offset}";
                }
                else if (totalMinutes > 0)
                {
                    return $"UTC+{timeZone.Offset}";
                }
                else
                {
                    return "UTC±00:00";
                }
            }
        }

        /// <summary>
        /// Return a <see cref="string"/> that contain all countries that use the standard time of the given time zone.
        /// </summary>
        /// <param name="timeZone">The time zone that contain the countries.</param>
        /// <param name="query">The <see cref="Query"/> that should match a country that use standard time.</param>
        /// <param name="maxLength">The maximum length of the result <see cref="string"/>.</param>
        /// <returns>A <see cref="string"/> with countries that use the standard time.</returns>
        private static string GetStandardCountries(in OneTimeZone timeZone, Query? query, in int maxLength)
        {
            IEnumerable<string> countries;

            if (query is null || string.IsNullOrWhiteSpace(query.Search))
            {
                countries = timeZone.CountriesStandard;
            }
            else
            {
                countries = timeZone.CountriesStandard.Where(x => x.Contains(query.Search, StringComparison.CurrentCultureIgnoreCase));
            }

            var stringBuilder = new StringBuilder();
            var lastEntry = countries.LastOrDefault();

            foreach (var country in countries)
            {
                stringBuilder.Append(country);

                if (country != lastEntry)
                {
                    stringBuilder.Append(',');
                    stringBuilder.Append(' ');
                }
            }

            // To many countries (first pass) => remove extra info
            if (stringBuilder.Length > maxLength)
            {
                stringBuilder.Clear();

                foreach (var country in countries)
                {
                    var extraInfoStart = country.IndexOf('(', StringComparison.InvariantCultureIgnoreCase);
                    if (extraInfoStart > 0)
                    {
                        stringBuilder.Append(country[..extraInfoStart]);
                    }
                    else
                    {
                        stringBuilder.Append(country);
                    }

                    if (country != lastEntry)
                    {
                        stringBuilder.Append(',');
                        stringBuilder.Append(' ');
                    }
                }
            }

            // To many countries (second pass) => remove extra info and cut country length
            if (stringBuilder.Length > maxLength)
            {
                foreach (var country in countries)
                {
                    var extraInfoStart = country.IndexOf('(', StringComparison.InvariantCultureIgnoreCase);
                    if (extraInfoStart > 0)
                    {
                        stringBuilder.SaveAppend(country[..extraInfoStart], maxLength: 5);
                    }
                    else
                    {
                        stringBuilder.SaveAppend(country, maxLength: 5);
                    }

                    if (country != lastEntry)
                    {
                        stringBuilder.Append(',');
                        stringBuilder.Append(' ');
                    }
                }
            }

            stringBuilder.CutTooLong(maxLength);

            return stringBuilder.ToString();
        }

        /// <summary>
        /// Return a <see cref="string"/> that contain all countries that use the daylight saving time of the given time zone.
        /// </summary>
        /// <param name="timeZone">The time zone that contain the countries.</param>
        /// <param name="query">The <see cref="Query"/>v</param>
        /// <param name="maxLength">The maximum length of the result <see cref="string"/>.</param>
        /// <returns>A <see cref="string"/> with countries that use the daylight saving time.</returns>
        private static string GetDaylightCountries(in OneTimeZone timeZone, Query? query, in int maxLength)
        {
            IEnumerable<string> countries;

            if (query is null || string.IsNullOrWhiteSpace(query.Search))
            {
                countries = timeZone.CountriesDaylight;
            }
            else
            {
                countries = timeZone.CountriesDaylight.Where(x => x.Contains(query.Search, StringComparison.CurrentCultureIgnoreCase));
            }

            var stringBuilder = new StringBuilder();
            var lastEntry = countries.LastOrDefault();

            foreach (var country in countries)
            {
                stringBuilder.Append(country);

                if (country != lastEntry)
                {
                    stringBuilder.Append(',');
                    stringBuilder.Append(' ');
                }
            }

            // To many countries (first pass) => remove extra info
            if (stringBuilder.Length > maxLength)
            {
                stringBuilder.Clear();

                foreach (var country in countries)
                {
                    var extraInfoStart = country.IndexOf('(', StringComparison.InvariantCultureIgnoreCase);
                    if (extraInfoStart > 0)
                    {
                        stringBuilder.Append(country[..extraInfoStart]);
                    }
                    else
                    {
                        stringBuilder.Append(country);
                    }

                    if (country != lastEntry)
                    {
                        stringBuilder.Append(',');
                        stringBuilder.Append(' ');
                    }
                }
            }

            // To many countries (second pass) => remove extra info and cut country length
            if (stringBuilder.Length > maxLength)
            {
                foreach (var country in countries)
                {
                    var extraInfoStart = country.IndexOf('(', StringComparison.InvariantCultureIgnoreCase);
                    if (extraInfoStart > 0)
                    {
                        stringBuilder.SaveAppend(country[..extraInfoStart], maxLength: 5);
                    }
                    else
                    {
                        stringBuilder.SaveAppend(country, maxLength: 5);
                    }

                    if (country != lastEntry)
                    {
                        stringBuilder.Append(',');
                        stringBuilder.Append(' ');
                    }
                }
            }

            stringBuilder.CutTooLong(maxLength);

            return stringBuilder.ToString();
        }

        /// <summary>
        /// Return a <see cref="string"/> that contain all countries of the given time zone.
        /// </summary>
        /// <param name="timeZone">The time zone that contain the countries.</param>
        /// <param name="query">The <see cref="Query"/>v</param>
        /// <param name="maxLength">The maximum length of the result <see cref="string"/>.</param>
        /// <returns>A <see cref="string"/> with countries.</returns>
        private static string GetAllCountries(in OneTimeZone timeZone, Query? query, int maxLength)
        {
            IEnumerable<string> countries;

            if (query is null || string.IsNullOrWhiteSpace(query.Search))
            {
                countries = timeZone.CountriesDaylight.Concat(timeZone.CountriesStandard);
            }
            else
            {
                countries = timeZone.CountriesDaylight.Where(x => x.Contains(query.Search, StringComparison.CurrentCultureIgnoreCase))
                    .Concat(timeZone.CountriesStandard.Where(x => x.Contains(query.Search, StringComparison.CurrentCultureIgnoreCase)));
            }

            var stringBuilder = new StringBuilder();
            var lastEntry = countries.LastOrDefault();

            foreach (var country in countries)
            {
                stringBuilder.Append(country);

                if (country != lastEntry)
                {
                    stringBuilder.Append(',');
                    stringBuilder.Append(' ');
                }
            }

            // To many countries (first pass) => remove extra info
            if (stringBuilder.Length > maxLength)
            {
                stringBuilder.Clear();

                foreach (var country in countries)
                {
                    var extraInfoStart = country.IndexOf('(', StringComparison.InvariantCultureIgnoreCase);
                    if (extraInfoStart > 0)
                    {
                        stringBuilder.Append(country[..extraInfoStart]);
                    }
                    else
                    {
                        stringBuilder.Append(country);
                    }

                    if (country != lastEntry)
                    {
                        stringBuilder.Append(',');
                        stringBuilder.Append(' ');
                    }
                }
            }

            // To many countries (second pass) => remove extra info and cut country length
            if (stringBuilder.Length > maxLength)
            {
                foreach (var country in countries)
                {
                    var extraInfoStart = country.IndexOf('(', StringComparison.InvariantCultureIgnoreCase);
                    if (extraInfoStart > 0)
                    {
                        stringBuilder.SaveAppend(country[..extraInfoStart], maxLength: 5);
                    }
                    else
                    {
                        stringBuilder.SaveAppend(country, maxLength: 5);
                    }

                    if (country != lastEntry)
                    {
                        stringBuilder.Append(',');
                        stringBuilder.Append(' ');
                    }
                }
            }

            stringBuilder.CutTooLong(maxLength);

            return stringBuilder.ToString();
        }

        private static bool MatchShortcuts(in OneTimeZone timeZone, Query query)
        {
            var result = timeZone.Shortcut.Contains(query.Search, StringComparison.CurrentCultureIgnoreCase);
            return result;
        }

        private static bool MatchNames(in OneTimeZone timeZone, Query query)
        {
            var result = timeZone.Name.Contains(query.Search, StringComparison.CurrentCultureIgnoreCase)
                || timeZone.MilitaryName.Contains(query.Search, StringComparison.CurrentCultureIgnoreCase);

            return result;
        }

        private static bool MatchDaylightNames(in OneTimeZone timeZone, Query query)
        {
            var result = timeZone.TimeNamesDaylight?.Any(x => x.Contains(query.Search, StringComparison.CurrentCultureIgnoreCase)) == true;
            return result;
        }

        private static bool MatchStandardNames(in OneTimeZone timeZone, Query query)
        {
            var result = timeZone.TimeNamesDaylight?.Any(x => x.Contains(query.Search, StringComparison.CurrentCultureIgnoreCase)) == true;
            return result;
        }

        private static bool MatchOffset(in OneTimeZone timeZone, Query query)
        {
            // allow search for "-x:xx"
            if (query.Search.StartsWith('-') && timeZone.Offset.StartsWith('-'))
            {
                if (timeZone.Offset.ElementAtOrDefault(1) == '0')
                {
                    if (timeZone.Offset[2..].StartsWith(query.Search[1..], StringComparison.InvariantCultureIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            // allow search for "+xx:xx"
            if (query.Search.StartsWith('+') && timeZone.Offset.StartsWith(query.Search[1..], StringComparison.InvariantCultureIgnoreCase))
            {
                return true;
            }

            return false;
        }

        private static bool MatchStandardCountries(in OneTimeZone timeZone, Query query)
        {
            var result = timeZone.CountriesStandard?.Any(x => x.Contains(query.Search, StringComparison.CurrentCultureIgnoreCase)) == true;
            return result;
        }

        private static bool MatchDaylightCountries(in OneTimeZone timeZone, Query query)
        {
            var result = timeZone.CountriesDaylight?.Any(x => x.Contains(query.Search, StringComparison.CurrentCultureIgnoreCase)) == true;
            return result;
        }
    }
}
