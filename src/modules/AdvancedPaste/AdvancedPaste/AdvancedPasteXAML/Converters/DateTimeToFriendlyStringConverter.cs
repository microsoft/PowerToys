// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Data;
using Microsoft.Windows.ApplicationModel.Resources;

namespace AdvancedPaste.Converters
{
    public sealed partial class DateTimeToFriendlyStringConverter : IValueConverter
    {
        private static readonly ResourceLoader _resources = new ResourceLoader();

        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is not DateTimeOffset dto)
            {
                return string.Empty;
            }

            // Use local times to calculate relative values and formatting
            var now = DateTimeOffset.Now;
            var localValue = dto.ToLocalTime();
            var culture = !string.IsNullOrEmpty(language)
                ? new CultureInfo(language)
                : CultureInfo.CurrentCulture;

            var delta = now - localValue;

            // Future dates: fall back to date/time formatting
            if (delta < TimeSpan.Zero)
            {
                return FormatDateAndTime(localValue, culture);
            }

            // < 1 minute
            if (delta.TotalSeconds < 60)
            {
                return _resources.GetString("Relative_JustNow"); // "Just now"
            }

            // < 60 minutes
            if (delta.TotalMinutes < 60)
            {
                var mins = (int)Math.Round(delta.TotalMinutes);
                if (mins <= 1)
                {
                    return _resources.GetString("Relative_MinuteAgo"); // "1 minute ago"
                }

                var fmt = _resources.GetString("Relative_MinutesAgo_Format"); // "{0} minutes ago"
                return string.Format(culture, fmt, mins);
            }

            // Same calendar day → "Today, {time}"
            var today = now.Date;
            if (localValue.Date == today)
            {
                var time = localValue.ToString("t", culture); // localized short time
                var fmt = _resources.GetString("Relative_Today_TimeFormat"); // "Today, {0}"
                return string.Format(culture, fmt, time);
            }

            // Yesterday → "Yesterday, {time}"
            if (localValue.Date == today.AddDays(-1))
            {
                var time = localValue.ToString("t", culture);
                var fmt = _resources.GetString("Relative_Yesterday_TimeFormat"); // "Yesterday, {0}"
                return string.Format(culture, fmt, time);
            }

            // Within last 7 days → "{Weekday}, {time}"
            if (delta.TotalDays < 7)
            {
                var weekday = localValue.ToString("dddd", culture); // localized weekday
                var time = localValue.ToString("t", culture);
                var fmt = _resources.GetString("Relative_Weekday_TimeFormat"); // "{0}, {1}"
                return string.Format(culture, fmt, weekday, time);
            }

            // Older → localized date + time
            return FormatDateAndTime(localValue, culture);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
            => throw new NotSupportedException();

        private static string FormatDateAndTime(DateTimeOffset localValue, CultureInfo culture)
        {
            // Use localized short date + short time
            var date = localValue.ToString("d", culture);
            var time = localValue.ToString("t", culture);
            var fmt = _resources.GetString("Relative_Date_TimeFormat"); // "{0}, {1}"
            return string.Format(culture, fmt, date, time);
        }
    }
}
