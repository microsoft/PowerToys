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
            var results = new List<Result>();
            var utcNow = DateTime.UtcNow;

            foreach (var timeZone in timeZones)
            {
                if (TimeZoneInfoMatchQuery(timeZone, search))
                {
                    results.AddRange(GetResults(timeZone, utcNow, search, iconPath));
                }
            }

            var orderResults = results.OrderBy(result => result.Title);
            return orderResults;
        }

        /// <summary>
        /// Check if the given <see cref="OneTimeZone"/> contains a value that match the given <see cref="Query"/> .
        /// </summary>
        /// <param name="timeZone">The <see cref="OneTimeZone"/> to check.</param>
        /// <param name="search">The <see cref="Query"/> that should match.</param>
        /// <returns><see langword="true"/> if it's match, otherwise <see langword="false"/>.</returns>
        private static bool TimeZoneInfoMatchQuery(OneTimeZone timeZone, string search)
        {
            // allow search for "-x:xx"
            if (search.StartsWith('-') && timeZone.OffsetString.StartsWith('-'))
            {
                if (timeZone.OffsetString.ElementAtOrDefault(1) == '0')
                {
                    if (timeZone.OffsetString[2..].StartsWith(search[1..], StringComparison.InvariantCultureIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            // allow search for "+xx:xx"
            if (search.StartsWith('+') && timeZone.OffsetString.StartsWith(search[1..], StringComparison.InvariantCultureIgnoreCase))
            {
                return true;
            }

            // "-1x:xx" match here
            if (timeZone.OffsetString.Contains(search, StringComparison.InvariantCultureIgnoreCase))
            {
                return true;
            }

            if (timeZone.Name.Contains(search, StringComparison.CurrentCultureIgnoreCase))
            {
                return true;
            }

            if (timeZone.MilitaryName.Contains(search, StringComparison.CurrentCultureIgnoreCase))
            {
                return true;
            }

            if (timeZone.Shortcut.Contains(search, StringComparison.CurrentCultureIgnoreCase))
            {
                return true;
            }

            if (timeZone.TimeNamesDaylight != null
            && timeZone.TimeNamesDaylight.Any(x => x.Contains(search, StringComparison.CurrentCultureIgnoreCase)))
            {
                return true;
            }

            if (timeZone.TimeNamesStandard != null
            && timeZone.TimeNamesStandard.Any(x => x.Contains(search, StringComparison.CurrentCultureIgnoreCase)))
            {
                return true;
            }

            if (timeZone.CountriesStandard != null
            && timeZone.CountriesStandard.Any(x => x.Contains(search, StringComparison.CurrentCultureIgnoreCase)))
            {
                return true;
            }

            if (timeZone.CountriesDaylight != null
            && timeZone.CountriesDaylight.Any(x => x.Contains(search, StringComparison.CurrentCultureIgnoreCase)))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Return a list with <see cref="Result"/> based on the given <see cref="OneTimeZone"/>.
        /// </summary>
        /// <param name="timeZone">The <see cref="OneTimeZone"/> that contain the information for the <see cref="Result"/>.</param>
        /// <param name="utcNow">The current time in UTC for the <see cref="Result"/>.</param>
        /// <returns>A list with <see cref="Result"/>.</returns>
        private static IEnumerable<Result> GetResults(OneTimeZone timeZone, DateTime utcNow, string search, string iconPath)
        {
            // TODO: revisit time zone names
            // TODO: revisit standard time names
            // TODO: revisit DST time names
            // TODO: add time zone shortcuts
            var results = new Collection<Result>();

            var countries = GetCountries(timeZone, search, maxLength: 100);
            if (countries.Length > 0)
            {
                var title = GetTitle(timeZone, utcNow, false);

                results.Add(new Result
                {
                    ContextData = timeZone,
                    IcoPath = iconPath,
                    SubTitle = countries,
                    Title = title,
                    ToolTipData = new ToolTipData(title, GetToolTip(timeZone)),
                });
            }

            var dstCountries = GetDstCountries(timeZone, search, maxLength: 100);
            if (dstCountries.Length > 0)
            {
                var title = GetTitle(timeZone, utcNow, true);

                results.Add(new Result
                {
                    ContextData = timeZone,
                    IcoPath = iconPath,
                    SubTitle = dstCountries,
                    Title = title,
                    ToolTipData = new ToolTipData(title, GetDstToolTip(timeZone)),
                });
            }

            return results;
        }

        private static DateTime GetTimeInTimeZone(OneTimeZone timeZone, DateTime utcNow, bool daylightSavingTime)
        {
            foreach (var timeZoneInfo in TimeZoneInfo.GetSystemTimeZones())
            {
                if (timeZoneInfo.BaseUtcOffset == timeZone.Offset
                && timeZoneInfo.SupportsDaylightSavingTime == daylightSavingTime)
                {
                    return TimeZoneInfo.ConvertTime(utcNow, timeZoneInfo);
                }
            }

            // Fall-back
            var result = utcNow + timeZone.Offset;
            return result;
        }

        private static string GetTitle(OneTimeZone timeZone, DateTime utcNow, bool daylightSavingTime)
        {
            var timeInZoneTime = GetTimeInTimeZone(timeZone, utcNow, daylightSavingTime);
            var timeZoneNames = GetNames(timeZone, maxLength: 50);

            return $"{timeInZoneTime:HH:mm:ss} - {timeZoneNames}";
        }

        private static string GetToolTip(OneTimeZone timeZone)
        {
            var countries = GetCountries(timeZone, search: string.Empty, maxLength: int.MaxValue);
            var names = GetNames(timeZone, maxLength: int.MaxValue);

            var stringBuilder = new StringBuilder();

            stringBuilder.Append(Resources.Offset).Append(':').Append(' ').AppendLine(timeZone.OffsetString);
            stringBuilder.Append(Resources.UseDst).Append(':').Append(' ').AppendLine(Resources.No);
            stringBuilder.AppendLine(string.Empty);
            stringBuilder.Append(Resources.Names).Append(':').Append(' ').AppendLine(names);
            stringBuilder.AppendLine(string.Empty);
            stringBuilder.Append(Resources.Countries).Append(':').Append(' ').AppendLine(countries);

            return stringBuilder.ToString();
        }

        private static string GetDstToolTip(OneTimeZone timeZone)
        {
            var dstCountries = GetDstCountries(timeZone, search: string.Empty, maxLength: int.MaxValue);
            var names = GetNames(timeZone, maxLength: int.MaxValue);

            var stringBuilder = new StringBuilder();

            stringBuilder.Append(Resources.Offset).Append(':').Append(' ').AppendLine(timeZone.OffsetString);
            stringBuilder.Append(Resources.UseDst).Append(':').Append(' ').AppendLine(Resources.Yes);
            stringBuilder.AppendLine(string.Empty);
            stringBuilder.Append(Resources.Names).Append(':').Append(' ').AppendLine(names);
            stringBuilder.AppendLine(string.Empty);
            stringBuilder.Append(Resources.CountriesWithDst).Append(':').Append(' ').AppendLine(dstCountries);

            return stringBuilder.ToString();
        }

        private static string GetNames(OneTimeZone timeZone, int maxLength)
        {
            var names = new List<string>();

            if (!string.IsNullOrWhiteSpace(timeZone.Name))
            {
                names.Add(timeZone.Name);
            }

            if (!string.IsNullOrWhiteSpace(timeZone.MilitaryName))
            {
                names.Add(timeZone.MilitaryName);
            }

            if (timeZone.TimeNamesStandard != null)
            {
                names.AddRange(timeZone.TimeNamesStandard);
            }

            if (timeZone.TimeNamesDaylight != null)
            {
                names.AddRange(timeZone.TimeNamesDaylight);
            }

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

            // To many names (first pass) => cut name length
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

        private static string GetCountries(OneTimeZone timeZone, string search, int maxLength)
        {
            IEnumerable<string> countries;

            // TODO: translate country names
            if (string.IsNullOrWhiteSpace(search))
            {
                countries = timeZone.CountriesStandard;
            }
            else
            {
                countries = timeZone.CountriesStandard.Where(x => x.Contains(search, StringComparison.CurrentCultureIgnoreCase));
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

        private static string GetDstCountries(OneTimeZone timeZone, string search, int maxLength)
        {
            IEnumerable<string> countries;

            // TODO: translate country names
            if (string.IsNullOrWhiteSpace(search))
            {
                countries = timeZone.CountriesDaylight;
            }
            else
            {
                countries = timeZone.CountriesDaylight.Where(x => x.Contains(search, StringComparison.CurrentCultureIgnoreCase));
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
    }
}
