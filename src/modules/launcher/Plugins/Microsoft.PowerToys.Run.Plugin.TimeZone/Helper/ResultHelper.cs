// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
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
        internal static IEnumerable<Result> GetResults(IEnumerable<Classes.TimeZone> timeZones, string search, string iconPath)
        {
            var results = new Collection<Result>();
            var utcNow = DateTime.UtcNow;

            foreach (var timeZone in timeZones)
            {
                if (TimeZoneInfoMatchQuery(timeZone, search))
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
        /// <param name="search">The <see cref="Query"/> that should match.</param>
        /// <returns><see langword="true"/> if it's match, otherwise <see langword="false"/>.</returns>
        private static bool TimeZoneInfoMatchQuery(Classes.TimeZone timeZone, string search)
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
        private static Result GetResult(Classes.TimeZone timeZone, DateTime utcNow, string iconPath)
        {
            // TODO: respect DST on time calculation
            // TODO: DST into SubTitle and toolTip
            // TODO: add timezones
            // TODO: add shortcuts

            var timeInZoneTime = GetTimeInTimeZone(timeZone, utcNow);

            var title = string.IsNullOrWhiteSpace(timeZone.Name) ? timeZone.Offset : timeZone.Name;

            var toolTip = $"{Environment.NewLine}{Resources.StandardName}: {timeZone.Name}"
                        + $"{Environment.NewLine}{Resources.Offset}: {timeZone.Offset}";

            var result = new Result
            {
                ContextData = timeZone,
                IcoPath = iconPath,
                SubTitle = $"{Resources.CurrentTime}: {timeInZoneTime:HH:mm:ss}",
                Title = title,
                ToolTipData = new ToolTipData(title, toolTip),
            };

            return result;
        }

        private static DateTime GetTimeInTimeZone(Classes.TimeZone timeZone, DateTime utcNow)
        {
            string offset;
            DateTime result;

            if (timeZone.Offset.StartsWith("-", StringComparison.InvariantCultureIgnoreCase))
            {
                offset = timeZone.Offset.Substring(1);
            }
            else
            {
                offset = timeZone.Offset;
            }

            var offsetSplit = offset.Split(':');

            int.TryParse(offsetSplit.FirstOrDefault(), out var hours);
            int.TryParse(offsetSplit.LastOrDefault(), out var minutes);

            if (timeZone.Offset.StartsWith("-", StringComparison.InvariantCultureIgnoreCase))
            {
                result = utcNow.AddHours(-hours).AddMinutes(-minutes);
            }
            else
            {
                result = utcNow.AddHours(hours).AddMinutes(minutes);
            }

            return result;
        }
    }
}
