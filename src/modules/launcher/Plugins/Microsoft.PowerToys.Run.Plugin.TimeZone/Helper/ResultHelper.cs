// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.PowerToys.Run.Plugin.TimeZone.Classes;
using Microsoft.PowerToys.Run.Plugin.TimeZone.Extensions;
using Microsoft.PowerToys.Run.Plugin.TimeZone.Properties;
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
        /// <param name="timeZones">A list with all possible time zones.</param>
        /// <param name="options">Additional options to limit the results.</param>
        /// <param name="query">The <see cref="Query"/> to filter the <see cref="Results"/>.</param>
        /// <param name="iconPath">The path to the icon that is used for each result.</param>
        /// <returns>A list with <see cref="Result"/>s.</returns>
        internal static IEnumerable<Result> GetResults(in IEnumerable<TimeZoneProperties> timeZones, in TimeZoneSettings options, in Query query, in string iconPath)
        {
            var results = new List<Result>();
            var dateTime = DateTime.UtcNow;

            foreach (var timeZone in timeZones)
            {
                if (MatchTimeZoneShortcut(timeZone, query)
                || MatchStandardTimeShortcuts(timeZone, query)
                || MatchDaylightTimeShortcuts(timeZone, query)
                || MatchTimeZoneNames(timeZone, query)
                || MatchStandardTimeNames(timeZone, query)
                || MatchDaylightTimeNames(timeZone, query)
                || MatchStandardCountries(timeZone, query)
                || MatchDaylightCountries(timeZone, query)
                || MatchOffset(timeZone, query))
                {
                    results.AddRange(GetResults(timeZone, options, query, iconPath, dateTime));
                }
            }

            var orderResults = results.OrderBy(result => result.Title);
            return orderResults;
        }

        /// <summary>
        /// Return a list with <see cref="Result"/> based on the given time zone.
        /// </summary>
        /// <param name="timeZoneProperties">The time zone that contain the information for the <see cref="Result"/>.</param>
        /// <param name="options">Additional options to limit the results.</param>
        /// <param name="query">The <see cref="Query"/> that should match.</param>
        /// <param name="iconPath">The path to the icon that is used for each result.</param>
        /// <param name="dateTime">The current time in UTC for the <see cref="Result"/>.</param>
        /// <returns>A list with <see cref="Result"/>.</returns>
        internal static IEnumerable<Result> GetResults(in TimeZoneProperties timeZoneProperties, in TimeZoneSettings options, in Query query, in string iconPath, in DateTime dateTime)
        {
            var results = new Collection<Result>();

            var standardTitleResult = GetTitle(timeZoneProperties, options, query, dateTime, false);
            var daylightTitleResult = GetTitle(timeZoneProperties, options, query, dateTime, true);

            if (standardTitleResult.Equals(daylightTitleResult))
            {
                results.Add(new Result
                {
                    ContextData = GetTimeInTimeZone(timeZoneProperties, dateTime, false),
                    IcoPath = iconPath,
                    Title = standardTitleResult.ToString(),
                    SubTitle = GetAllCountries(timeZoneProperties, query, maxLength: 100).ToString(),
                    ToolTipData = new ToolTipData(standardTitleResult.ToString(), GetAllToolTip(timeZoneProperties, options).ToString()),
                });

                return results;
            }

            if (MatchStandardTimeShortcuts(timeZoneProperties, query)
            || MatchStandardTimeNames(timeZoneProperties, query)
            || MatchStandardCountries(timeZoneProperties, query))
            {
                var hasCountries = GetStandardCountries(timeZoneProperties, null, int.MaxValue).Length > 0;
                if (!hasCountries)
                {
                    return results;
                }

                results.Add(new Result
                {
                    ContextData = GetTimeInTimeZone(timeZoneProperties, dateTime, false),
                    IcoPath = iconPath,
                    SubTitle = GetStandardCountries(timeZoneProperties, query, maxLength: 100).ToString(),
                    Title = standardTitleResult.ToString(),
                    ToolTipData = new ToolTipData(standardTitleResult.ToString(), GetStandardToolTip(timeZoneProperties, options).ToString()),
                });
            }

            if (MatchDaylightTimeShortcuts(timeZoneProperties, query)
            || MatchDaylightTimeNames(timeZoneProperties, query)
            || MatchDaylightCountries(timeZoneProperties, query))
            {
                var hasCountries = GetDaylightCountries(timeZoneProperties, null, int.MaxValue).Length > 0;
                if (!hasCountries)
                {
                    return results;
                }

                results.Add(new Result
                {
                    ContextData = GetTimeInTimeZone(timeZoneProperties, dateTime, true),
                    IcoPath = iconPath,
                    SubTitle = GetDaylightCountries(timeZoneProperties, query, maxLength: 100).ToString(),
                    Title = daylightTitleResult.ToString(),
                    ToolTipData = new ToolTipData(daylightTitleResult.ToString(), GetDaylightToolTip(timeZoneProperties, options).ToString()),
                });
            }

            return results;
        }

        /// <summary>
        /// Return the current local time of the given time zone.
        /// </summary>
        /// <param name="timeZoneProperties">The time zone that contain all information.</param>
        /// <param name="dateTime">The current time in UTC.</param>
        /// <param name="daylightSavingTime">indicate that the result is for a time zone that use a daylight saving time.</param>
        /// <returns>The current local time in a time zone.</returns>
        internal static DateTime GetTimeInTimeZone(in TimeZoneProperties timeZoneProperties, in DateTime dateTime, in bool daylightSavingTime)
        {
            foreach (var timeZoneInfo in TimeZoneInfo.GetSystemTimeZones())
            {
                if (timeZoneInfo.BaseUtcOffset == timeZoneProperties.OffsetAsTimeSpan
                && timeZoneInfo.SupportsDaylightSavingTime == daylightSavingTime)
                {
                    return TimeZoneInfo.ConvertTime(dateTime, timeZoneInfo);
                }
            }

            // Fall-back
            var result = dateTime + timeZoneProperties.OffsetAsTimeSpan;
            return result;
        }

        /// <summary>
        /// Return the title for the given time zone.
        /// </summary>
        /// <param name="timeZoneProperties">The time zone that contain all information.</param>
        /// <param name="timeZoneSettings">Additional options to limit the results.</param>
        /// <param name="query">The <see cref="Query"/> that should match.</param>
        /// <param name="dateTime">The current time in UTC.</param>
        /// <param name="daylightSavingTime">indicate that the result is for a time zone that use a daylight saving time.</param>
        /// <returns>A title for a time zone.</returns>
        internal static StringBuilder GetTitle(in TimeZoneProperties timeZoneProperties, in TimeZoneSettings timeZoneSettings, in Query query, in DateTime dateTime, in bool daylightSavingTime)
        {
            var stringBuilder = new StringBuilder();

            var timeInZoneTime = GetTimeInTimeZone(timeZoneProperties, dateTime, daylightSavingTime);
            var timeZoneNames = GetNames(timeZoneProperties, timeZoneSettings, query, maxLength: 50);

            stringBuilder.AppendFormat(CultureInfo.CurrentCulture, "{0:HH:mm:ss}", timeInZoneTime);
            stringBuilder.Append(' ');
            stringBuilder.Append('-');
            stringBuilder.Append(' ');
            stringBuilder.Append(timeZoneNames);

            return stringBuilder;
        }

        /// <summary>
        /// Return a tool-tip for the given time zone with countries that use the standard time.
        /// </summary>
        /// <param name="timeZoneProperties">The time zone that contain all information.</param>
        /// <param name="timeZoneSettings">Additional options to limit the results.</param>
        /// <returns>A tool-tip with countries that use the standard time.</returns>
        internal static StringBuilder GetStandardToolTip(in TimeZoneProperties timeZoneProperties, in TimeZoneSettings timeZoneSettings)
        {
            var countries = GetStandardCountries(timeZoneProperties, null, maxLength: int.MaxValue);
            var names = GetNames(timeZoneProperties, timeZoneSettings, null, maxLength: int.MaxValue);
            var shortcuts = GetStandardShortcuts(timeZoneProperties);

            if (!string.IsNullOrWhiteSpace(timeZoneProperties.Shortcut))
            {
                shortcuts.Append(',');
                shortcuts.Append(' ');
                shortcuts.Append(timeZoneProperties.Shortcut);
            }

            var stringBuilder = new StringBuilder();

            stringBuilder.Append(Resources.Offset).Append(':').Append(' ').AppendLine(timeZoneProperties.Offset);
            stringBuilder.Append(Resources.UseDst).Append(':').Append(' ').AppendLine(Resources.No);
            stringBuilder.AppendLine(string.Empty);
            stringBuilder.Append(Resources.Names).Append(':').Append(' ').Append(names);
            stringBuilder.AppendLine(string.Empty);
            stringBuilder.AppendLine(string.Empty);
            stringBuilder.Append(Resources.Shortcuts).Append(':').Append(' ').Append(shortcuts);
            stringBuilder.AppendLine(string.Empty);
            stringBuilder.AppendLine(string.Empty);
            stringBuilder.Append(Resources.Countries).Append(':').Append(' ').Append(countries);

            return stringBuilder;
        }

        /// <summary>
        /// Return a tool-tip for the given time zone with countries that use the daylight saving time.
        /// </summary>
        /// <param name="timeZoneProperties">The time zone that contain all information.</param>
        /// <param name="timeZoneSettings">Additional options to limit the type of the names.</param>
        /// <returns>A tool-tip with countries that use the daylight saving time.</returns>
        internal static StringBuilder GetDaylightToolTip(in TimeZoneProperties timeZoneProperties, in TimeZoneSettings timeZoneSettings)
        {
            var dstCountries = GetDaylightCountries(timeZoneProperties, null, maxLength: int.MaxValue);
            var names = GetNames(timeZoneProperties, timeZoneSettings, null, maxLength: int.MaxValue);
            var shortcuts = GetDaylightShortcuts(timeZoneProperties);

            if (!string.IsNullOrWhiteSpace(timeZoneProperties.Shortcut))
            {
                shortcuts.Append(',');
                shortcuts.Append(' ');
                shortcuts.Append(timeZoneProperties.Shortcut);
            }

            var stringBuilder = new StringBuilder();

            stringBuilder.Append(Resources.Offset).Append(':').Append(' ').AppendLine(timeZoneProperties.Offset);
            stringBuilder.Append(Resources.UseDst).Append(':').Append(' ').AppendLine(Resources.Yes);
            stringBuilder.AppendLine(string.Empty);
            stringBuilder.Append(Resources.Names).Append(':').Append(' ').Append(names);
            stringBuilder.AppendLine(string.Empty);
            stringBuilder.AppendLine(string.Empty);
            stringBuilder.Append(Resources.Shortcuts).Append(':').Append(' ').Append(shortcuts);
            stringBuilder.AppendLine(string.Empty);
            stringBuilder.AppendLine(string.Empty);
            stringBuilder.Append(Resources.CountriesWithDst).Append(':').Append(' ').Append(dstCountries);

            return stringBuilder;
        }

        /// <summary>
        /// Return a tool-tip for the given time zone with countries.
        /// </summary>
        /// <param name="timeZoneProperties">The time zone that contain all information.</param>
        /// <param name="timeZoneSettings">Additional options to limit the type of the names.</param>
        /// <returns>A tool-tip with countries.</returns>
        internal static StringBuilder GetAllToolTip(in TimeZoneProperties timeZoneProperties, in TimeZoneSettings timeZoneSettings)
        {
            var countries = GetStandardCountries(timeZoneProperties, null, maxLength: int.MaxValue);
            var dstCountries = GetDaylightCountries(timeZoneProperties, null, maxLength: int.MaxValue);
            var names = GetNames(timeZoneProperties, timeZoneSettings, null, maxLength: int.MaxValue);
            var shortcuts = GetStandardShortcuts(timeZoneProperties);
            var dstShortcuts = GetDaylightShortcuts(timeZoneProperties);

            if (dstShortcuts.Length > 0)
            {
                shortcuts.Append(',');
                shortcuts.Append(' ');
                shortcuts.Append(dstShortcuts);
            }

            if (!string.IsNullOrWhiteSpace(timeZoneProperties.Shortcut))
            {
                shortcuts.Append(',');
                shortcuts.Append(' ');
                shortcuts.Append(timeZoneProperties.Shortcut);
            }

            var stringBuilder = new StringBuilder();

            stringBuilder.Append(Resources.Offset).Append(':').Append(' ').AppendLine(timeZoneProperties.Offset);
            stringBuilder.Append(Resources.UseDst).Append(':').Append(' ').AppendLine(Resources.Yes);
            stringBuilder.AppendLine(string.Empty);
            stringBuilder.Append(Resources.Names).Append(':').Append(' ').Append(names);
            stringBuilder.AppendLine(string.Empty);
            stringBuilder.AppendLine(string.Empty);
            stringBuilder.Append(Resources.Shortcuts).Append(':').Append(' ').Append(shortcuts);
            stringBuilder.AppendLine(string.Empty);
            stringBuilder.AppendLine(string.Empty);
            stringBuilder.Append(Resources.Countries).Append(':').Append(' ').Append(countries);
            stringBuilder.AppendLine(string.Empty);
            stringBuilder.AppendLine(string.Empty);
            stringBuilder.Append(Resources.CountriesWithDst).Append(':').Append(' ').Append(dstCountries);

            return stringBuilder;
        }

        /// <summary>
        /// Return all names of the given time zone that match the given query.
        /// </summary>
        /// <param name="timeZoneProperties">The time zone that contain a hand of names.</param>
        /// <param name="timeZoneSettings">Additional options to limit the type of the names.</param>
        /// <param name="query">The query that should match.</param>
        /// <param name="maxLength">The maximum length of the result.</param>
        /// <returns>All know names of the given time zone.</returns>
        internal static StringBuilder GetNames(in TimeZoneProperties timeZoneProperties, in TimeZoneSettings timeZoneSettings, Query? query, in int maxLength)
        {
            var allNames = new List<string>();

            if (!string.IsNullOrWhiteSpace(timeZoneProperties.Name) && timeZoneSettings.ShowTimeZoneNames)
            {
                allNames.Add(timeZoneProperties.Name);
            }

            if (!string.IsNullOrWhiteSpace(timeZoneProperties.MilitaryName) && timeZoneSettings.ShowMilitaryTimeZoneNames)
            {
                allNames.Add(timeZoneProperties.MilitaryName);
            }

            if (timeZoneProperties.TimeNamesStandard != null && timeZoneSettings.ShowTimeZoneNames)
            {
                allNames.AddRange(timeZoneProperties.TimeNamesStandard);
            }

            if (timeZoneProperties.TimeNamesDaylight != null && timeZoneSettings.ShowTimeZoneNames)
            {
                allNames.AddRange(timeZoneProperties.TimeNamesDaylight);
            }

            IEnumerable<string> names;

            if (query is null || string.IsNullOrWhiteSpace(query.Search))
            {
                names = allNames;
            }
            else if (MatchStandardCountries(timeZoneProperties, query) || MatchDaylightCountries(timeZoneProperties, query))
            {
                names = allNames;
            }
            else if (MatchStandardTimeShortcuts(timeZoneProperties, query) || MatchDaylightTimeShortcuts(timeZoneProperties, query))
            {
                var matches = new Collection<string>();

                foreach (var name in allNames)
                {
                    var matchAll = query.Search.All(x => name.Contains(x, StringComparison.CurrentCultureIgnoreCase));
                    if (matchAll)
                    {
                        matches.Add(name);
                    }
                }

                names = matches;
            }
            else
            {
                names = allNames.Where(x => x.Contains(query.Search, StringComparison.CurrentCultureIgnoreCase));
            }

            var stringBuilder = new StringBuilder();

            if (names.Any())
            {
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
                    stringBuilder.Replace(Resources.StandardTime, Resources.StandardTimeShortcut);
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
            }
            else
            {
                // only when we don't have found any names so we
                stringBuilder.Append("UTC");

                var totalMinutes = timeZoneProperties.OffsetAsTimeSpan.TotalMinutes;
                if (totalMinutes < 0)
                {
                    stringBuilder.Append(timeZoneProperties.Offset);
                }
                else if (totalMinutes > 0)
                {
                    stringBuilder.Append('+');
                    stringBuilder.Append(timeZoneProperties.Offset);
                }
                else
                {
                    stringBuilder.Append("±00:00");
                }
            }

            return stringBuilder;
        }

        /// <summary>
        /// Return all standard time name shortcuts of the given time zone.
        /// </summary>
        /// <param name="timeZoneProperties">The time zone that contain a hand of names.</param>
        /// <returns>All standard time name shortcuts of the given time zone.</returns>
        internal static StringBuilder GetStandardShortcuts(in TimeZoneProperties timeZoneProperties)
        {
            var stringBuilder = new StringBuilder();
            var lastEntry = timeZoneProperties.ShortcutsStandard.LastOrDefault();

            foreach (var name in timeZoneProperties.ShortcutsStandard)
            {
                stringBuilder.Append(name);

                if (name != lastEntry)
                {
                    stringBuilder.Append(',');
                    stringBuilder.Append(' ');
                }
            }

            return stringBuilder;
        }

        /// <summary>
        /// Return all know daylight time name shortcuts of the given time zone.
        /// </summary>
        /// <param name="timeZoneProperties">The time zone that contain a hand of names.</param>
        /// <returns>All know daylight time name shortcuts of the given time zone.</returns>
        internal static StringBuilder GetDaylightShortcuts(in TimeZoneProperties timeZoneProperties)
        {
            var stringBuilder = new StringBuilder();
            var lastEntry = timeZoneProperties.ShortcutsDaylight.LastOrDefault();

            foreach (var name in timeZoneProperties.ShortcutsDaylight)
            {
                stringBuilder.Append(name);

                if (name != lastEntry)
                {
                    stringBuilder.Append(',');
                    stringBuilder.Append(' ');
                }
            }

            return stringBuilder;
        }

        /// <summary>
        /// Return all countries that use the standard time of the given time zone that match the given query.
        /// </summary>
        /// <param name="timeZoneProperties">The time zone that contain the countries.</param>
        /// <param name="query">The <see cref="Query"/> that should match a country that use standard time.</param>
        /// <param name="maxLength">The maximum length of the result.</param>
        /// <returns>All countries that use the standard time of the given time zone.</returns>
        internal static StringBuilder GetStandardCountries(in TimeZoneProperties timeZoneProperties, Query? query, in int maxLength)
        {
            IEnumerable<string> countries;

            if (query is null || string.IsNullOrWhiteSpace(query.Search))
            {
                countries = timeZoneProperties.CountriesStandard;
            }
            else if (MatchStandardTimeShortcuts(timeZoneProperties, query))
            {
                var matches = new Collection<string>();

                foreach (var name in timeZoneProperties.CountriesStandard)
                {
                    var matchAll = query.Search.All(x => name.Contains(x, StringComparison.CurrentCultureIgnoreCase));
                    if (matchAll)
                    {
                        matches.Add(name);
                    }
                }

                countries = matches;
            }
            else
            {
                countries = timeZoneProperties.CountriesStandard.Where(x => x.Contains(query.Search, StringComparison.CurrentCultureIgnoreCase));
            }

            // When the search query don't match a country, show all countries
            if (countries is null || !countries.Any())
            {
                countries = timeZoneProperties.CountriesStandard;
            }

            var stringBuilder = new StringBuilder();
            var lastEntry = countries.LastOrDefault();

            foreach (var country in countries.Distinct())
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

            return stringBuilder;
        }

        /// <summary>
        /// Return all countries that use the daylight saving time of the given time zone that match the given query
        /// </summary>
        /// <param name="timeZoneProperties">The time zone that contain the countries.</param>
        /// <param name="query">The <see cref="Query"/> that should match a country that use daylight time.</param>
        /// <param name="maxLength">The maximum length of the result.</param>
        /// <returns>All countries that use the daylight saving time of the given time zone.</returns>
        internal static StringBuilder GetDaylightCountries(in TimeZoneProperties timeZoneProperties, Query? query, in int maxLength)
        {
            IEnumerable<string> countries;

            if (query is null || string.IsNullOrWhiteSpace(query.Search))
            {
                countries = timeZoneProperties.CountriesDaylight;
            }
            else if (MatchDaylightTimeShortcuts(timeZoneProperties, query))
            {
                var matches = new Collection<string>();

                foreach (var name in timeZoneProperties.CountriesDaylight)
                {
                    var matchAll = query.Search.All(x => name.Contains(x, StringComparison.CurrentCultureIgnoreCase));
                    if (matchAll)
                    {
                        matches.Add(name);
                    }
                }

                countries = matches;
            }
            else
            {
                countries = timeZoneProperties.CountriesDaylight.Where(x => x.Contains(query.Search, StringComparison.CurrentCultureIgnoreCase));
            }

            // When the search query don't match a country, show all countries
            if (countries is null || !countries.Any())
            {
                countries = timeZoneProperties.CountriesDaylight;
            }

            var stringBuilder = new StringBuilder();
            var lastEntry = countries.LastOrDefault();

            foreach (var country in countries.Distinct())
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

            return stringBuilder;
        }

        /// <summary>
        /// Return all countries of the given time zone that match the given query.
        /// </summary>
        /// <param name="timeZoneProperties">The time zone that contain the countries.</param>
        /// <param name="query">The <see cref="Query"/> that should match a country that use standard or daylight time.</param>
        /// <param name="maxLength">The maximum length of the result.</param>
        /// <returns>All countries of the given time zone.</returns>
        internal static StringBuilder GetAllCountries(in TimeZoneProperties timeZoneProperties, Query? query, in int maxLength)
        {
            IEnumerable<string> countries;

            if (query is null || string.IsNullOrWhiteSpace(query.Search))
            {
                countries = timeZoneProperties.CountriesDaylight
                    .Concat(timeZoneProperties.CountriesStandard);
            }
            else if (MatchDaylightTimeShortcuts(timeZoneProperties, query) || MatchStandardTimeShortcuts(timeZoneProperties, query))
            {
                var matches = new Collection<string>();

                foreach (var name in timeZoneProperties.CountriesDaylight.Concat(timeZoneProperties.CountriesStandard))
                {
                    var matchAll = query.Search.All(x => name.Contains(x, StringComparison.CurrentCultureIgnoreCase));
                    if (matchAll)
                    {
                        matches.Add(name);
                    }
                }

                countries = matches;
            }
            else
            {
                countries = timeZoneProperties.CountriesDaylight.Where(x => x.Contains(query.Search, StringComparison.CurrentCultureIgnoreCase))
                    .Concat(timeZoneProperties.CountriesStandard.Where(x => x.Contains(query.Search, StringComparison.CurrentCultureIgnoreCase)));
            }

            // When the search query don't match a country, show all countries
            if (countries is null || !countries.Any())
            {
                countries = timeZoneProperties.CountriesDaylight
                    .Concat(timeZoneProperties.CountriesStandard);
            }

            var stringBuilder = new StringBuilder();
            var lastEntry = countries.LastOrDefault();

            foreach (var country in countries.Distinct())
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

            return stringBuilder;
        }

        /// <summary>
        /// Indicate that the given query match the time zone shortcut of the given time zone.
        /// </summary>
        /// <param name="timeZoneProperties">The time zone to check.</param>
        /// <param name="query">The query that should match.</param>
        /// <returns><see langword="true"/>if the query match, otherwise <see langword="false"/>.</returns>
        internal static bool MatchTimeZoneShortcut(in TimeZoneProperties timeZoneProperties, Query query)
        {
            var result = timeZoneProperties.Shortcut.Contains(query.Search, StringComparison.CurrentCultureIgnoreCase);
            return result;
        }

        /// <summary>
        /// Indicate that the given query match one of the time zone names of the given time zone.
        /// </summary>
        /// <param name="timeZoneProperties">The time zone to check.</param>
        /// <param name="query">The query that should match.</param>
        /// <returns><see langword="true"/>if the query match, otherwise <see langword="false"/>.</returns>
        internal static bool MatchTimeZoneNames(in TimeZoneProperties timeZoneProperties, Query query)
        {
            var result = timeZoneProperties.Name.Contains(query.Search, StringComparison.CurrentCultureIgnoreCase)
                || timeZoneProperties.MilitaryName.Contains(query.Search, StringComparison.CurrentCultureIgnoreCase);

            return result;
        }

        /// <summary>
        /// Indicate that the given query match the offset of the given time zone.
        /// </summary>
        /// <param name="timeZoneProperties">The time zone to check.</param>
        /// <param name="query">The query that should match.</param>
        /// <returns><see langword="true"/>if the query match, otherwise <see langword="false"/>.</returns>
        internal static bool MatchOffset(in TimeZoneProperties timeZoneProperties, Query query)
        {
            // allow search for "-xx:xx"
            if (timeZoneProperties.Offset.StartsWith('-') && query.Search.StartsWith('-'))
            {
                if (timeZoneProperties.Offset[1..].Contains(query.Search[1..], StringComparison.InvariantCultureIgnoreCase))
                {
                    return true;
                }
            }

            // allow search for "+xx:xx"
            if (!timeZoneProperties.Offset.StartsWith('-') && query.Search.StartsWith('+'))
            {
                if (timeZoneProperties.Offset.Contains(query.Search[1..], StringComparison.InvariantCultureIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Indicate that the given query match one of the standard time names of the given time zone.
        /// </summary>
        /// <param name="timeZoneProperties">The time zone to check.</param>
        /// <param name="query">The query that should match.</param>
        /// <returns><see langword="true"/>if the query match, otherwise <see langword="false"/>.</returns>
        internal static bool MatchStandardTimeNames(in TimeZoneProperties timeZoneProperties, Query query)
        {
            var result = timeZoneProperties.TimeNamesStandard.Any(x => x.Contains(query.Search, StringComparison.CurrentCultureIgnoreCase));
            return result;
        }

        /// <summary>
        /// Indicate that the given query match one of the daylight time names of the given time zone.
        /// </summary>
        /// <param name="timeZoneProperties">The time zone to check.</param>
        /// <param name="query">The query that should match.</param>
        /// <returns><see langword="true"/>if the query match, otherwise <see langword="false"/>.</returns>
        internal static bool MatchDaylightTimeNames(in TimeZoneProperties timeZoneProperties, Query query)
        {
            var result = timeZoneProperties.TimeNamesDaylight.Any(x => x.Contains(query.Search, StringComparison.CurrentCultureIgnoreCase));
            return result;
        }

        /// <summary>
        /// Indicate that the given query match one of the countries that use the standard time of the given time zone.
        /// </summary>
        /// <param name="timeZoneProperties">The time zone to check.</param>
        /// <param name="query">The query that should match.</param>
        /// <returns><see langword="true"/>if the query match, otherwise <see langword="false"/>.</returns>
        internal static bool MatchStandardCountries(in TimeZoneProperties timeZoneProperties, Query query)
        {
            var result = timeZoneProperties.CountriesStandard.Any(x => x.Contains(query.Search, StringComparison.CurrentCultureIgnoreCase));
            return result;
        }

        /// <summary>
        /// Indicate that the given query match one of the countries that use the daylight time of the given time zone.
        /// </summary>
        /// <param name="timeZoneProperties">The time zone to check.</param>
        /// <param name="query">The query that should match.</param>
        /// <returns><see langword="true"/>if the query match, otherwise <see langword="false"/>.</returns>
        internal static bool MatchDaylightCountries(in TimeZoneProperties timeZoneProperties, Query query)
        {
            var result = timeZoneProperties.CountriesDaylight.Any(x => x.Contains(query.Search, StringComparison.CurrentCultureIgnoreCase));
            return result;
        }

        /// <summary>
        /// Indicate that the given query match the time zone shortcut of the given time zone.
        /// </summary>
        /// <param name="timeZoneProperties">The time zone to check.</param>
        /// <param name="query">The query that should match.</param>
        /// <returns><see langword="true"/>if the query match, otherwise <see langword="false"/>.</returns>
        internal static bool MatchStandardTimeShortcuts(in TimeZoneProperties timeZoneProperties, Query query)
        {
            var result = timeZoneProperties.ShortcutsStandard.Any(x => x.Contains(query.Search, StringComparison.CurrentCultureIgnoreCase));
            return result;
        }

        /// <summary>
        /// Indicate that the given query match the time zone shortcut of the given time zone.
        /// </summary>
        /// <param name="timeZoneProperties">The time zone to check.</param>
        /// <param name="query">The query that should match.</param>
        /// <returns><see langword="true"/>if the query match, otherwise <see langword="false"/>.</returns>
        internal static bool MatchDaylightTimeShortcuts(in TimeZoneProperties timeZoneProperties, Query query)
        {
            var result = timeZoneProperties.ShortcutsDaylight.Any(x => x.Contains(query.Search, StringComparison.CurrentCultureIgnoreCase));
            return result;
        }
    }
}
