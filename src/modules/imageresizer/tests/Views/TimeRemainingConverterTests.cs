// Copyright (c) Brice Lambson
// The Brice Lambson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.  Code forked from Brice Lambson's https://github.com/bricelam/ImageResizer/

using System;
using System.Globalization;
using ImageResizer.Properties;
using Xunit;
using Xunit.Extensions;

namespace ImageResizer.Views
{
    public class TimeRemainingConverterTests
    {
        [Theory]
        [InlineData("HourMinute", 1, 1, 0)]
        [InlineData("HourMinutes", 1, 2, 0)]
        [InlineData("HoursMinute", 2, 1, 0)]
        [InlineData("HoursMinutes", 2, 2, 0)]
        [InlineData("MinuteSecond", 0, 1, 1)]
        [InlineData("MinuteSeconds", 0, 1, 2)]
        [InlineData("MinutesSecond", 0, 2, 1)]
        [InlineData("MinutesSeconds", 0, 2, 2)]
        [InlineData("Second", 0, 0, 1)]
        [InlineData("Seconds", 0, 0, 2)]
        public void Convert_works(string resource, int hours, int minutes, int seconds)
        {
            var timeRemaining = new TimeSpan(hours, minutes, seconds);
            var converter = new TimeRemainingConverter();

            var result = converter.Convert(
                timeRemaining,
                targetType: null,
                parameter: null,
                CultureInfo.InvariantCulture);

            Assert.Equal(
                string.Format(
                    Resources.ResourceManager.GetString("Progress_TimeRemaining_" + resource),
                    hours,
                    minutes,
                    seconds),
                result);
        }
    }
}
