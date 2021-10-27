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
        /// Check if the given <see cref="OneTimeZone"/> contains a value that match the given <see cref="Query"/> .
        /// </summary>
        /// <param name="timeZone">The <see cref="OneTimeZone"/> to check.</param>
        /// <param name="search">The <see cref="Query"/> that should match.</param>
        /// <returns><see langword="true"/> if it's match, otherwise <see langword="false"/>.</returns>
        private static bool TimeZoneInfoMatchQuery(OneTimeZone timeZone, string search)
        {
            // allow search for "-x:xx"
            if (search.StartsWith('-') && timeZone.Offset.StartsWith('-'))
            {
                if (timeZone.Offset.ElementAtOrDefault(1) == '0')
                {
                    if (timeZone.Offset.Substring(2).StartsWith(search.Substring(1), StringComparison.InvariantCultureIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            // allow search for "+xx:xx"
            if (search.StartsWith('+') && timeZone.Offset.StartsWith(search.Substring(1), StringComparison.InvariantCultureIgnoreCase))
            {
                return true;
            }

            // "-1x:xx" match here
            if (timeZone.Offset.Contains(search, StringComparison.InvariantCultureIgnoreCase))
            {
                return true;
            }

            if (timeZone.Names != null
            & timeZone.Names.Any(x => x.Contains(search, StringComparison.CurrentCultureIgnoreCase)))
            {
                return true;
            }

            if (timeZone.Shortcut.Contains(search, StringComparison.InvariantCultureIgnoreCase))
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
        /// Return a <see cref="Result"/> based on the given <see cref="OneTimeZone"/>.
        /// </summary>
        /// <param name="timeZone">The <see cref="OneTimeZone"/> that contain the information for the <see cref="Result"/>.</param>
        /// <param name="utcNow">The current time in UTC for the <see cref="Result"/>.</param>
        /// <returns>A <see cref="Result"/>.</returns>
        private static Result GetResult(OneTimeZone timeZone, DateTime utcNow, string search, string iconPath)
        {
            // TODO: revisit time zone names
            // TODO: add standard and DST time zone names
            // TODO: add shortcuts
            var title = GetTitle(timeZone, search, utcNow);

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

        private static string GetTitle(OneTimeZone timeZone, string search, DateTime utcNow)
        {
            var timeInZoneTime = GetTimeInTimeZone(timeZone, utcNow);
            var timeZoneNames = GetNames(timeZone, search, maxLength: 50);

            return $"{timeInZoneTime:HH:mm:ss} - {timeZoneNames}";
        }

        private static string GetToolTip(OneTimeZone timeZone)
        {
            var useDst = timeZone.DaylightSavingTime ? Resources.Yes : Resources.No;
            var fullTimeOffset = GetFullOffset(timeZone);
            var countries = GetCountries(timeZone, search: string.Empty, maxLength: int.MaxValue);
            var names = GetNames(timeZone, search: string.Empty, maxLength: int.MaxValue);

            var stringBuilder = new StringBuilder();

            stringBuilder.Append(Resources.Names).Append(':').Append(' ').AppendLine(names);
            stringBuilder.Append(Resources.Offset).Append(':').Append(' ').AppendLine(fullTimeOffset);
            stringBuilder.Append(Resources.DaylightSavingTime).Append(':').Append(' ').AppendLine(useDst);
            stringBuilder.AppendLine(string.Empty);
            stringBuilder.Append(Resources.Countries).Append(':').Append(' ').AppendLine(countries);

            return stringBuilder.ToString();
        }

        private static (byte hours, byte minutes) GetHoursAndMinutes(OneTimeZone timeZone)
        {
            string offset;

            if (timeZone.Offset.StartsWith("-", StringComparison.InvariantCultureIgnoreCase))
            {
                offset = timeZone.Offset[1..];
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
                countries = timeZone.Countries.Where(x => x.Contains(search, StringComparison.CurrentCultureIgnoreCase));
                if (!countries.Any())
                {
                    countries = timeZone.Countries;
                }
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

            // To many countries (third pass) => cut text length
            if (stringBuilder.Length > maxLength)
            {
                stringBuilder.Length = maxLength - 3;
                stringBuilder.Append('.');
                stringBuilder.Append('.');
                stringBuilder.Append('.');
            }

            return stringBuilder.ToString();
        }

        private static string GetNames(OneTimeZone timeZone, string search, int maxLength)
        {
            IEnumerable<string> names;

            // TODO: translate country names
            if (string.IsNullOrWhiteSpace(search))
            {
                names = timeZone.Names;
            }
            else
            {
                names = timeZone.Names.Where(x => x.Contains(search, StringComparison.CurrentCultureIgnoreCase));
                if (!names.Any())
                {
                    names = timeZone.Names;
                }
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

            // To many names (third pass) => cut text length
            if (stringBuilder.Length > maxLength)
            {
                stringBuilder.Length = maxLength - 3;
                stringBuilder.Append('.');
                stringBuilder.Append('.');
                stringBuilder.Append('.');
            }

            return stringBuilder.ToString();
        }

        private static string GetFullOffset(OneTimeZone timeZone)
        {
            var (hours, minutes) = GetHoursAndMinutes(timeZone);

            string result;

            if (timeZone.Offset.StartsWith('-'))
            {
                result = $"-{hours}:{minutes:00}";
            }
            else
            {
                result = $"+{hours}:{minutes:00}";
            }

            return result;
        }
    }
}
