// Copyright (c) Brice Lambson
// The Brice Lambson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.  Code forked from Brice Lambson's https://github.com/bricelam/ImageResizer/

using System;
using System.Globalization;
using System.Text;
using System.Windows.Data;
using ImageResizer.Properties;

namespace ImageResizer.Views
{
    [ValueConversion(typeof(TimeSpan), typeof(string))]
    internal class TimeRemainingConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var timeRemaining = (TimeSpan)value;

            var builder = new StringBuilder("Progress_TimeRemaining_");

            if (timeRemaining.Hours != 0)
            {
                builder.Append(timeRemaining.Hours == 1 ? "Hour" : "Hours");
            }

            if (timeRemaining.Hours != 0 || timeRemaining.Minutes > 0)
            {
                builder.Append(timeRemaining.Minutes == 1 ? "Minute" : "Minutes");
            }

            if (timeRemaining.Hours == 0)
            {
                builder.Append(timeRemaining.Seconds == 1 ? "Second" : "Seconds");
            }

            return string.Format(
                culture,
                Resources.ResourceManager.GetString(builder.ToString(), culture),
                timeRemaining.Hours,
                timeRemaining.Minutes,
                timeRemaining.Seconds);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
