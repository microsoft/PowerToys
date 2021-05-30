// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.PowerToys.Run.Plugin.TimeZone.Properties;
using Mono.Collections.Generic;
using Wox.Plugin;
using Wox.Plugin.Logger;

namespace Microsoft.PowerToys.Run.Plugin.TimeZone.Helper
{
    /// <summary>
    /// Helper class to easier work with results
    /// </summary>
    internal static class ResultHelper
    {
        private static IEnumerable<TimeZoneInfoExtended> _allTimeZones = Enumerable.Empty<TimeZoneInfoExtended>();

        internal static void CollectAllTimeZones()
        {
            // TODO: remove after JSON
            var timeZoneInfos = TimeZoneInfo.GetSystemTimeZones();
            var allTimeZones = new List<TimeZoneInfoExtended>();
            foreach (var timeZone in timeZoneInfos)
            {
                var extendedTimeZone = new TimeZoneInfoExtended(timeZone);
                allTimeZones.Add(extendedTimeZone);
            }

            _allTimeZones = allTimeZones;
        }

        /// <summary>
        /// Return a list of <see cref="Result"/>s based on the given <see cref="Query"/>.
        /// </summary>
        /// <param name="query">The <see cref="Query"/> to filter the <see cref="Results"/>.</param>
        /// <returns>A list with <see cref="Result"/>s.</returns>
        internal static IEnumerable<Result> GetResults(Query query, string iconPath)
        {
            var results = new Collection<Result>();
            var utcNow = DateTime.UtcNow;

            foreach (var timeZone in _allTimeZones)
            {
                if (TimeZoneInfoMatchQuery(timeZone, query))
                {
                    var result = GetResult(timeZone, utcNow, iconPath);
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
        /// <param name="query">The <see cref="Query"/> that should match.</param>
        /// <returns><see langword="true"/> if it's match, otherwise <see langword="false"/>.</returns>
        private static bool TimeZoneInfoMatchQuery(TimeZoneInfoExtended timeZone, Query query)
        {
            if (timeZone.Offset.Contains(query.Search, StringComparison.InvariantCultureIgnoreCase))
            {
                return true;
            }

            if (timeZone.StandardName.Contains(query.Search, StringComparison.CurrentCultureIgnoreCase))
            {
                return true;
            }

            if (timeZone.StandardShortcut != null
            && timeZone.StandardShortcut.Contains(query.Search, StringComparison.InvariantCultureIgnoreCase))
            {
                return true;
            }

            if (timeZone.DaylightName != null
            && timeZone.DaylightName.Contains(query.Search, StringComparison.CurrentCultureIgnoreCase))
            {
                return true;
            }

            if (timeZone.DaylightShortcut != null
            && timeZone.DaylightShortcut.Contains(query.Search, StringComparison.InvariantCultureIgnoreCase))
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
        private static Result GetResult(TimeZoneInfoExtended timeZone, DateTime utcNow, string iconPath)
        {
            var title = GetTitle(timeZone, utcNow);
            var timeInTimeZone = timeZone.ConvertTime(utcNow);

            var toolTip = $"{Resources.StandardName}: {timeZone.StandardName}"
                + $"{Environment.NewLine}{Resources.DaylightName}: {timeZone.DaylightName}"
                + $"{Environment.NewLine}{Resources.DisplayName}: {timeZone.DisplayName}"
                + $"{Environment.NewLine}{Resources.Offset}: {timeZone.Offset}";

            var result = new Result
            {
                ContextData = timeZone,
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
        /// <param name="timeZone">The <see cref="TimeZoneInfoExtended"/> for the title.</param>
        /// <param name="utcNow">The current time in UTC, only need for time zones that support daylight time.</param>
        /// <returns>A title for a <see cref="Result"/> or <see cref="ToolTipData"/>.</returns>
        private static string GetTitle(TimeZoneInfoExtended timeZone, DateTime utcNow)
        {
            if (string.IsNullOrEmpty(timeZone.DaylightName))
            {
                return timeZone.StandardName;
            }

            if (!timeZone.IsDaylightSavingTime(utcNow))
            {
                return timeZone.StandardName;
            }

            return timeZone.DaylightName;
        }

        // TODO: Remove after JSON is used
        internal static void SaveJson()
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            };

            try
            {
                using var fileStream = File.Create("D:\\TimeZones.json");
                using var streamWriter = new StreamWriter(fileStream);

                var json = JsonSerializer.Serialize(_allTimeZones, options);
                streamWriter.WriteLine(json);
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception exception)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                Log.Exception("uh", exception, typeof(Main));
            }
        }
    }
}
