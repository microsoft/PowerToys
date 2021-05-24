// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
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
        /// <param name="query">The <see cref="Query"/> to filter the <see cref="Results"/>.</param>
        /// <returns>A list with <see cref="Result"/>s.</returns>
        internal static IEnumerable<Result> GetResults(Query query, string iconPath)
        {
            var searchFor = GetSerchString(query);
            var timeZoneInfos = TimeZoneInfo.GetSystemTimeZones();
            var results = new Collection<Result>();
            var utcNow = DateTime.UtcNow;

            foreach (var timeZoneInfo in timeZoneInfos)
            {
                if (TimeZoneInfoMatchQuery(timeZoneInfo, searchFor))
                {
                    var result = GetResult(timeZoneInfo, utcNow, iconPath);
                    results.Add(result);
                }
            }

            var orderResults = results.OrderBy(result => result.Title);
            return orderResults;
        }

        /// <summary>
        /// Return the string to search, based on the given <see cref="Query"/>.
        /// </summary>
        /// <param name="query">The <see cref="Query"/> that contain the string to search.</param>
        /// <returns>A string for a search.</returns>
        private static string GetSerchString(Query query)
        {
            var secondChar = query.Search.ElementAtOrDefault(1);

            if (secondChar == '0')
            {
                return query.Search;
            }

            if (!char.IsDigit(secondChar))
            {
                return query.Search;
            }

            // Allow the user to direct search for "+9", instead of "+09".
            var searchFor = query.Search;

            if (query.Search.StartsWith('+'))
            {
                searchFor = "+0" + query.Search.Substring(1);
            }

            if (query.Search.StartsWith('-'))
            {
                searchFor = "-0" + query.Search.Substring(1);
            }

            return searchFor;
        }

        /// <summary>
        /// Check if the given <see cref="TimeZoneInfo"/> contains a value that match the given <see cref="string"/> .
        /// </summary>
        /// <param name="timeZoneInfo">The <see cref="TimeZoneInfo"/> to check.</param>
        /// <param name="searchFor">The <see cref="string"/> that should match.</param>
        /// <returns><see langword="true"/> if it's match, otherwise <see langword="false"/>.</returns>
        private static bool TimeZoneInfoMatchQuery(TimeZoneInfo timeZoneInfo, string searchFor)
        {
            if (timeZoneInfo.DisplayName.Contains(searchFor, StringComparison.InvariantCultureIgnoreCase))
            {
                return true;
            }

            if (timeZoneInfo.StandardName.Contains(searchFor, StringComparison.InvariantCultureIgnoreCase))
            {
                return true;
            }

            if (timeZoneInfo.DaylightName.Contains(searchFor, StringComparison.InvariantCultureIgnoreCase))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Return a <see cref="Result"/> based on the given <see cref="TimeZoneInfo"/>.
        /// </summary>
        /// <param name="timeZoneInfo">The <see cref="TimeZoneInfo"/> that contain the information for the <see cref="Result"/>.</param>
        /// <param name="utcNow">The current time in UTC for the <see cref="Result"/>.</param>
        /// <returns>A <see cref="Result"/>.</returns>
        private static Result GetResult(TimeZoneInfo timeZoneInfo, DateTime utcNow, string iconPath)
        {
            var title = GetTitle(timeZoneInfo, utcNow);
            var timeInTimeZone = TimeZoneInfo.ConvertTime(utcNow, timeZoneInfo);

            var toolTip = $"{Resources.StandardName}: {timeZoneInfo.StandardName}"
                + $"{Environment.NewLine}{Resources.DaylightName}: {timeZoneInfo.DaylightName}"
                + $"{Environment.NewLine}{Resources.DisplayName}: {timeZoneInfo.DisplayName}"
                + $"{Environment.NewLine}{Resources.Offset}: {timeZoneInfo.BaseUtcOffset}";

            var result = new Result
            {
                ContextData = timeZoneInfo,
                IcoPath = iconPath,
                SubTitle = $"{Resources.CurrentTime}: {timeInTimeZone:HH:mm:ss}",
                Title = title,
                ToolTipData = new ToolTipData(title, toolTip),
            };

            return result;
        }

        /// <summary>
        /// Return the title for a <see cref="Result"/> and <see cref="ToolTipData"/>
        /// </summary>
        /// <param name="timeZoneInfo">The <see cref="TimeZoneInfo"/> for the title.</param>
        /// <param name="utcNow">The current time in UTC, only need for time zones that support daylight time.</param>
        /// <returns>A title for a <see cref="Result"/> or <see cref="ToolTipData"/>.</returns>
        private static string GetTitle(TimeZoneInfo timeZoneInfo, DateTime utcNow)
        {
            if (!timeZoneInfo.SupportsDaylightSavingTime)
            {
                return timeZoneInfo.StandardName;
            }

            if (!timeZoneInfo.IsDaylightSavingTime(utcNow))
            {
                return timeZoneInfo.StandardName;
            }

            return timeZoneInfo.DaylightName;
        }
    }
}
