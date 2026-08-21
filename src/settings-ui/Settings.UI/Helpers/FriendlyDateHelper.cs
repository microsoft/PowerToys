// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;

namespace Microsoft.PowerToys.Settings.UI.Helpers;

public static class FriendlyDateHelper
{
    /// <summary>
    /// Formats a <see cref="DateTime"/> as a friendly relative string.
    /// Today → "Today at 1:22 PM", Yesterday → "Yesterday at 3:45 PM",
    /// older dates fall back to the full culture-specific date/time format.
    /// </summary>
    public static string Format(DateTime? dateTime)
    {
        if (dateTime is not DateTime dt)
        {
            return string.Empty;
        }

        var resourceLoader = ResourceLoaderInstance.ResourceLoader;
        var today = DateTime.Now.Date;
        var time = dt.ToString("t", CultureInfo.CurrentCulture);

        if (dt.Date == today)
        {
            var fmt = resourceLoader.GetString("General_LastCheckedDate_TodayAt");
            if (!string.IsNullOrEmpty(fmt))
            {
                return string.Format(CultureInfo.CurrentCulture, fmt, time);
            }
        }

        if (dt.Date == today.AddDays(-1))
        {
            var fmt = resourceLoader.GetString("General_LastCheckedDate_YesterdayAt");
            if (!string.IsNullOrEmpty(fmt))
            {
                return string.Format(CultureInfo.CurrentCulture, fmt, time);
            }
        }

        return dt.ToString(CultureInfo.CurrentCulture);
    }
}
